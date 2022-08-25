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

using System;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie
{
    public class TrieException : Exception
    {
        public TrieException()
        {

        }

        public TrieException(string message) : base(message)
        {

        }

        public TrieException(string message, Exception inner) : base(message, inner)
        {
        }
    }


    public class MissingAccountNodeTrieException : TrieException
    {
        public Keccak Root { get; }

        public Keccak AccountHash { get; }

        public MissingAccountNodeTrieException(Keccak missingAccountHash, Keccak rootHash, Exception inner = null): base($"Missing account node {missingAccountHash} from root: {rootHash}", inner)
        {
            AccountHash = missingAccountHash;
            Root = rootHash;
        }
    }

    public class MissingStorageNodeTrieException : TrieException
    {
        public Keccak Root { get; set; }

        public Keccak AccountHash { get; }

        public Keccak StorageHash { get; }

        public MissingStorageNodeTrieException(Keccak missingStorageHash, Keccak accountHash, Keccak rootHash, Exception inner = null) : base($"Missing storage node {missingStorageHash} from root: {accountHash}, account {rootHash}", inner)
        {
            StorageHash = missingStorageHash;
            AccountHash = accountHash;
            Root = rootHash;
        }
    }
}
