// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public interface IVerkleStore : IStoreWithReorgBoundary
{
    public byte[] RootHash { get; set; }
    byte[]? GetLeaf(byte[] key);
    SuffixTree? GetStem(byte[] key);
    InternalNode? GetBranch(byte[] key);
    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetStem(byte[] stemKey, SuffixTree suffixTree);
    void SetBranch(byte[] branchKey, InternalNode internalNodeValue);
    void Flush(long blockNumber);
    void ReverseState();
    void ApplyDiffLayer(BatchChangeSet changeSet);

    byte[] GetStateRoot();
    void MoveToStateRoot(byte[] stateRoot);

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore);

    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock);

    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock);

}
