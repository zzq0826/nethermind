// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Utils;
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

    public Pedersen RootHash
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }
    public byte[]? GetLeaf(ReadOnlySpan<byte> key)
    {
        return _treeCache.GetLeaf(key, out var value) ? value : _persistentStore.GetLeaf(key);
    }
    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key)
    {
        return _treeCache.GetInternalNode(key, out var value) ? value : _persistentStore.GetInternalNode(key);
    }
    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
        _treeCache.SetLeaf(leafKey, leafValue);
    }
    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        _treeCache.SetInternalNode(internalNodeKey, internalNodeValue);
    }
    public void Flush(long blockNumber)
    {
        foreach (KeyValuePair<byte[], byte[]?> entry in _treeCache.LeafTable)
        {
            _persistentStore.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in _treeCache.InternalTable)
        {
            _persistentStore.SetInternalNode(entry.Key, entry.Value);
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
    public Pedersen GetStateRoot()
    {
        return new Pedersen(GetInternalNode(Array.Empty<byte>())?.InternalCommitment.Point.ToBytes().ToArray() ?? throw new InvalidOperationException());
    }
    public bool MoveToStateRoot(Pedersen stateRoot)
    {
        bool isMoved = _persistentStore.MoveToStateRoot(stateRoot);
        if (isMoved) _treeCache = new VerkleMemoryDb();
        return isMoved;
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

    public void Reset()
    {
        _treeCache.InternalTable.Clear();
        _treeCache.LeafTable.Clear();
    }
}
