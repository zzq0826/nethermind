// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public class VerkleTrieStore : IVerkleStore
{
    private readonly IVerkleStore _persistentStore;
    private VerkleMemoryDb _treeCache;

    public VerkleTrieStore(IVerkleStore verkleStore)
    {
        _persistentStore = verkleStore ?? throw new ArgumentNullException(nameof(verkleStore));
        _treeCache = new VerkleMemoryDb();
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    public byte[] RootHash
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }
    public byte[]? GetLeaf(byte[] key)
    {
        return _treeCache.GetLeaf(key, out var value) ? value : _persistentStore.GetLeaf(key);
    }
    public SuffixTree? GetStem(byte[] key)
    {
        return _treeCache.GetStem(key, out var value) ? value : _persistentStore.GetStem(key);
    }
    public InternalNode? GetBranch(byte[] key)
    {
        return _treeCache.GetBranch(key, out var value) ? value : _persistentStore.GetBranch(key);
    }
    public void SetLeaf(byte[] leafKey, byte[] leafValue)
    {
        _treeCache.SetLeaf(leafKey, leafValue);
    }
    public void SetStem(byte[] stemKey, SuffixTree suffixTree)
    {
        _treeCache.SetStem(stemKey, suffixTree);
    }
    public void SetBranch(byte[] branchKey, InternalNode internalNodeValue)
    {
        _treeCache.SetBranch(branchKey, internalNodeValue);
    }
    public void Flush(long blockNumber)
    {
        foreach (KeyValuePair<byte[], byte[]?> entry in _treeCache.LeafTable)
        {
            _persistentStore.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], SuffixTree?> entry in _treeCache.StemTable)
        {
            _persistentStore.SetStem(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in _treeCache.BranchTable)
        {
            _persistentStore.SetBranch(entry.Key, entry.Value);
        }
        _persistentStore.Flush(blockNumber);
        _treeCache = new VerkleMemoryDb();
    }
    public void ReverseState()
    {
        _treeCache = new VerkleMemoryDb();
        _persistentStore.ReverseState();
    }
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        _treeCache = new VerkleMemoryDb();
        _persistentStore.ApplyDiffLayer(changeSet);
    }
    public byte[] GetStateRoot()
    {
        return GetBranch(Array.Empty<byte>())?._internalCommitment.Point.ToBytes().ToArray() ?? throw new InvalidOperationException();
    }
    public void MoveToStateRoot(byte[] stateRoot)
    {
        _treeCache = new VerkleMemoryDb();
        _persistentStore.MoveToStateRoot(stateRoot);
    }
    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        throw new NotImplementedException();
    }
    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock)
    {
        _treeCache = new VerkleMemoryDb();
        return _persistentStore.GetForwardMergedDiff(fromBlock, toBlock);
    }
    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock)
    {
        _treeCache = new VerkleMemoryDb();
        return _persistentStore.GetReverseMergedDiff(fromBlock, toBlock);
    }
}
