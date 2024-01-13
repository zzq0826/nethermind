// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core.Crypto;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public interface IReadOnlyVerkleTrieStore : IVerkleTrieStore { }

public interface IVerkleTrieStore : IStoreWithReorgBoundary, IVerkleSyncTireStore
{
    Hash256 StateRoot { get; }

    bool HasStateForBlock(Hash256 stateRoot);
    bool MoveToStateRoot(Hash256 stateRoot);

    byte[]? GetLeaf(ReadOnlySpan<byte> key, Hash256? stateRoot = null);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key, Hash256? stateRoot = null);

    void InsertBatch(long blockNumber, VerkleMemoryDb memDb);

    void ApplyDiffLayer(BatchChangeSet changeSet);

    IReadOnlyVerkleTrieStore AsReadOnly(VerkleMemoryDb keyValueStore);
}
