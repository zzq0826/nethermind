// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.ComponentModel;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using System.IO;
using Nethermind.Core.Extensions;
using System.Buffers;

namespace Nethermind.State
{
    public class BlendStateTree : PatriciaTree
    {
        private readonly AccountDecoder _decoder = new();

        private static readonly Rlp EmptyAccountRlp = Rlp.Encode(Account.TotallyEmpty);

        [DebuggerStepThrough]
        public BlendStateTree()
            : base(new MemDb(), Keccak.EmptyTreeHash, true, true, NullLogManager.Instance)
        {
            TrieType = TrieType.State;
        }

        [DebuggerStepThrough]
        public BlendStateTree(ITrieStore? store, ILogManager? logManager)
            : base(store, Keccak.EmptyTreeHash, true, true, logManager)
        {
            TrieType = TrieType.State;
        }

        public void Commit(long blockNumber, bool skipRoot = false)
        {
            Commit(blockNumber, skipRoot, trackPath: true);
        }

        public Account? Get(Address address, Keccak? rootHash = null)
        {
            Keccak accountKeyHash = Keccak.Compute(address.Bytes);
            byte[]? bytes = TrieStore.LoadRlp(accountKeyHash);
            return bytes is not null ? _decoder.Decode(bytes.AsRlpStream()) : null;
        }

        public Account? GetDirect(Keccak accountAddressHash)
        {
            byte[]? bytes = TrieStore.LoadRlp(accountAddressHash);
            return bytes is not null ? _decoder.Decode(bytes.AsRlpStream()) : null;
        }

        [DebuggerStepThrough]
        internal Account? Get(Keccak keccak) // for testing
        {
            byte[]? bytes = Get(keccak.Bytes);
            if (bytes is null)
            {
                return null;
            }

            return _decoder.Decode(bytes.AsRlpStream());
        }

        public void Set(Address address, Account? account)
        {
            ValueKeccak keccak = ValueKeccak.Compute(address.Bytes);
            Rlp? data = account is null ? null : account.IsTotallyEmpty ? EmptyAccountRlp : Rlp.Encode(account);
            //TrieStore
            Set(keccak.BytesAsSpan, data);
        }

        [DebuggerStepThrough]
        public Rlp? Set(Keccak keccak, Account? account)
        {
            Rlp rlp = account is null ? null : account.IsTotallyEmpty ? EmptyAccountRlp : Rlp.Encode(account);
            //TrieStore
            Set(keccak.Bytes, rlp);
            return rlp;
        }
    }
}
