// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public interface IVerkleStore: IStoreWithReorgBoundary, IVerkleSyncTireStore
{
    public Pedersen StateRoot { get; }
    byte[]? GetLeaf(ReadOnlySpan<byte> key);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key);
    void Flush(long blockNumber, VerkleMemoryDb memDb);
    void ReverseState();
    void ApplyDiffLayer(BatchChangeSet changeSet);

    Pedersen GetStateRoot();
    bool MoveToStateRoot(Pedersen stateRoot);

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore);

    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock);

    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock);

    public void Reset();

}

public interface IVerkleStateStore: IVerkleState
{
    void Commit(long blockNumber);
    void Persist();
    bool MoveToStateRoot(byte[] stateRoot);
}

public interface IVerkleState: IReadOnlyVerkleState
{
    void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue);
    void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue);
}

public interface IReadOnlyVerkleState
{
    public Pedersen StateRoot { get; }
    byte[]? GetLeaf(ReadOnlySpan<byte> key);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key);
}

public interface IVerkleSyncTireStore
{
    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange,
        byte[] toRange, long blockNumber);

    public IEnumerable<PathWithSubTree> GetLeafRangeIterator(Stem fromRange, Stem toRange, Pedersen stateRoot, long bytes);
}

