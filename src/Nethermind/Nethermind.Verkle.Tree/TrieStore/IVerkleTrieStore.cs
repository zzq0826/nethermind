// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core.Crypto;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public interface IVerkleArchiveStore: IVerkleTrieStore
{
    public event EventHandler<InsertBatchCompletedV1>? InsertBatchCompletedV1;

    public event EventHandler<InsertBatchCompletedV2>? InsertBatchCompletedV2;
}

public interface IReadOnlyVerkleTrieStore : IVerkleTrieStore { }

public interface IVerkleTrieStore : IStoreWithReorgBoundary, IVerkleSyncTireStore, ISyncTrieStore
{
    Hash256 StateRoot { get; }

    bool HasStateForBlock(Hash256 stateRoot);
    bool MoveToStateRoot(Hash256 stateRoot);

    byte[]? GetLeaf(ReadOnlySpan<byte> key, Hash256? stateRoot = null);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key, Hash256? stateRoot = null);

    void InsertBatch(long blockNumber, VerkleMemoryDb memDb, bool skipRoot = false);

    void ApplyDiffLayer(BatchChangeSet changeSet);

    IReadOnlyVerkleTrieStore AsReadOnly(VerkleMemoryDb keyValueStore);

    ulong GetBlockNumber(Hash256 rootHash);
    public void InsertRootNodeAfterSyncCompletion(byte[] rootHash, long blockNumber);
    public void InsertSyncBatch(long blockNumber, VerkleMemoryDb batch);
}
