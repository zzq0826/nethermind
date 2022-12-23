// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Pruning
{
    public class ReadOnlyTrieBlendStore : IReadOnlyTrieBlendStore
    {
        private readonly TrieBlendStore _trieStore;
        private readonly IKeyValueStore? _readOnlyStore;
        private readonly IKeyValueStore? _readOnlyLeafStore;


        public ReadOnlyTrieBlendStore(
            TrieBlendStore trieStore,
            IKeyValueStore? readOnlyStore,
            IKeyValueStore? readOnlyLeafStore)
        {
            _trieStore = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
            _readOnlyStore = readOnlyStore;
            _readOnlyLeafStore = readOnlyLeafStore;
        }

        public byte[]? this[byte[] key] => _trieStore[key];

        public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
        {
            add { }
            remove { }
        }

        public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore)
        {
            return new ReadOnlyTrieBlendStore(_trieStore, keyValueStore, _readOnlyLeafStore);
        }

        public IReadOnlyTrieBlendStore AsReadOnly(IKeyValueStore? keyValueStore, IKeyValueStore? readOnlyLeafStore)
        {
            return new ReadOnlyTrieBlendStore(_trieStore, keyValueStore, readOnlyLeafStore);
        }

        public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo) { }

        public void Dispose() { }

        public TrieNode FindCachedOrUnknown(Keccak hash) => _trieStore.FindCachedOrUnknown(hash, true);

        public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root) { }

        public bool IsPersisted(Keccak keccak) => _trieStore.IsPersisted(keccak);

        public byte[]? LoadRlp(Keccak hash) => _trieStore.LoadRlp(hash, _readOnlyStore);

        public void SaveNodeDirectly(long blockNumber, byte[] fullPath, TrieNode trieNode, Keccak nodeHash = null) { }
    }
}
