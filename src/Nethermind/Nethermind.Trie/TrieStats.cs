//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie
{
    public class TrieStats
    {
        private const int Levels = 64;
        internal int _stateBranchCount;
        internal int _stateExtensionCount;
        internal int _accountCount;
        internal int _storageBranchCount;
        internal int _storageExtensionCount;
        internal int _storageLeafCount;
        internal int _codeCount;
        internal int _missingState;
        internal int _missingCode;
        internal int _missingStorage;
        internal long _storageSize;
        internal long _codeSize;
        internal long _stateSize;
        internal readonly int[] _stateLevels = new int[Levels];
        internal readonly int[] _storageLevels = new int[Levels];

        public int StateBranchCount => _stateBranchCount;

        public int StateExtensionCount => _stateExtensionCount;

        public int AccountCount => _accountCount;

        public int StorageBranchCount => _storageBranchCount;

        public int StorageExtensionCount => _storageExtensionCount;

        public int StorageLeafCount => _storageLeafCount;

        public int CodeCount => _codeCount;

        public int MissingState => _missingState;

        public int MissingCode => _missingCode;

        public int MissingStorage => _missingStorage;

        public int MissingNodes => MissingCode + MissingState + MissingStorage;

        public int StorageCount => StorageLeafCount + StorageExtensionCount + StorageBranchCount;

        public int StateCount => AccountCount + StateExtensionCount + StateBranchCount;

        public int NodesCount => StorageCount + StateCount + CodeCount;

        public long StorageSize => _storageSize;

        public long CodeSize => _codeSize;

        public long StateSize => _stateSize;

        public long Size => StateSize + StorageSize + CodeSize;

        public int[] StateLevels => _stateLevels;
        public int[] StorageLevels => _storageLevels;
        public int[] AllLevels
        {
            get
            {
                int[] result = new int[Levels];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = _stateLevels[i] + _storageLevels[i];
                }

                return result;
            }
        }

        public ConcurrentDictionary<Keccak, long> StorageSizes = new();
        public ConcurrentDictionary<int, long> StorageStats = new();

        public override string ToString()
        {
            SortedList<int, long> list = new(StorageStats);

            StringBuilder builder = new();
            builder.AppendLine("TRIE STATS");
            builder.AppendLine($"  SIZE {Size} (STATE {StateSize}, CODE {CodeSize}, STORAGE {StorageSize})");
            builder.AppendLine($"  ALL NODES {NodesCount} ({StateBranchCount + StorageBranchCount}|{StateExtensionCount + StorageExtensionCount}|{AccountCount + StorageLeafCount})");
            builder.AppendLine($"  STATE NODES {StateCount} ({StateBranchCount}|{StateExtensionCount}|{AccountCount})");
            builder.AppendLine($"  STORAGE NODES {StorageCount} ({StorageBranchCount}|{StorageExtensionCount}|{StorageLeafCount})");
            builder.AppendLine($"  ACCOUNTS {AccountCount} OF WHICH ({CodeCount}) ARE CONTRACTS");
            builder.AppendLine($"  MISSING {MissingNodes} (STATE {MissingState}, CODE {MissingCode}, STORAGE {MissingStorage})");
            builder.AppendLine($"  ALL LEVELS {string.Join(" | ", AllLevels.Select((x, i) => $"{i}:{x}"))}");
            builder.AppendLine($"  STATE LEVELS {string.Join(" | ", StateLevels.Select((x, i) => $"{i}:{x}"))}");
            builder.AppendLine($"  STORAGE LEVELS {string.Join(" | ", StorageLevels.Select((x, i) => $"{i}:{x}"))}");

            foreach (var item in list)
            {
                builder.AppendLine($"STORAGES up to {item.Key} Mb: {item.Value}");
            }
            return builder.ToString();
        }
    }
}
