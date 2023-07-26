// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.Interfaces;

public interface IVerkleTrieStore: IStoreWithReorgBoundary, IVerkleSyncTireStore
{
    VerkleCommitment StateRoot { get; }
    VerkleCommitment GetStateRoot();
    bool MoveToStateRoot(VerkleCommitment stateRoot);

    byte[]? GetLeaf(ReadOnlySpan<byte> key);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key);

    void Flush(long blockNumber, VerkleMemoryDb memDb);
    void Reset();

    void ReverseState();
    void ApplyDiffLayer(BatchChangeSet changeSet);
    VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock);
    VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock);

    ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore);
}
