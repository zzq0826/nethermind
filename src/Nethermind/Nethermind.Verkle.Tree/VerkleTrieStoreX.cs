// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public class VerkleTrieStoreX
{
    public long PersistentBlockNumber { get; protected set; }
    public byte[]? PersistedStateRoot { get; protected set; }

    private readonly VerkleMemoryDb _treeCache;
    private readonly VerkleMemoryDb _dirtyNodes;
    private readonly IVerkleStore PersistentStore;

    public VerkleTrieStoreX(IVerkleStore verkleStore)
    {
        PersistentBlockNumber = -1;
        PersistentStore = verkleStore ?? throw new ArgumentNullException(nameof(verkleStore));
        _treeCache = new VerkleMemoryDb();
        _dirtyNodes = new VerkleMemoryDb();
    }

    public byte[]? GetLeaf(byte[] key)
    {
        return _treeCache.GetLeaf(key, out byte[]? value) ? value : PersistentStore.GetLeaf(key);
    }
    public SuffixTree? GetStem(byte[] key)
    {
        return _treeCache.GetStem(key, out SuffixTree? value) ? value : PersistentStore.GetStem(key);
    }
    public InternalNode? GetBranch(byte[] key)
    {
        return _treeCache.GetBranch(key, out InternalNode? value) ? value : PersistentStore.GetBranch(key);
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

    public void Commit(long blockNumber)
    {
        foreach (KeyValuePair<byte[], byte[]?> entry in _treeCache.LeafTable)
        {
            _dirtyNodes.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], SuffixTree?> entry in _treeCache.StemTable)
        {
            _dirtyNodes.SetStem(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in _treeCache.BranchTable)
        {
            _dirtyNodes.SetBranch(entry.Key, entry.Value);
        }
        _treeCache.LeafTable.Clear();
        _treeCache.StemTable.Clear();
        _treeCache.BranchTable.Clear();
    }

}
