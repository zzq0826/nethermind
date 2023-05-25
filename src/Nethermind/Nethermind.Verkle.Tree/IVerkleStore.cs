// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public interface IVerkleStore: IStoreWithReorgBoundary
{
    public byte[] RootHash { get; set; }
    byte[]? GetLeaf(byte[] key);
    InternalNode? GetInternalNode(byte[] key);
    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetInternalNode(byte[] internalNodeKey, InternalNode internalNodeValue);
    void Flush(long blockNumber);
    void ReverseState();
    void ApplyDiffLayer(BatchChangeSet changeSet);

    byte[] GetStateRoot();
    bool MoveToStateRoot(byte[] stateRoot);

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore);

    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock);

    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock);

}

public interface IVerkleTree
{
    public byte[] StateRoot { get; set; }
    public bool MoveToStateRoot(byte[] stateRoot);
    public byte[]? Get(byte[] key);
    public void Insert(byte[] key, byte[] value);
    public void Commit();
    public void CommitTree(long blockNumber);
    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null);
    public void Accept(IVerkleTreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null);
}

public interface IVerkleStateStore: IVerkleState
{
    void Commit(long blockNumber);
    void Persist();
    bool MoveToStateRoot(byte[] stateRoot);
}

public interface IVerkleState: IReadOnlyVerkleState
{
    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetInternalNode(byte[] internalNodeKey, InternalNode internalNodeValue);
}

public interface IReadOnlyVerkleState
{
    public byte[] StateRoot { get; }
    byte[]? GetLeaf(byte[] key);
    InternalNode? GetInternalNode(byte[] key);
}

