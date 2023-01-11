// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Db;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.VerkleNodes;
using Nethermind.Verkle.VerkleStateDb;

namespace Nethermind.Verkle;

public class ReadOnlyVerkleStateStore: IVerkleStore
{
    private VerkleStateStore _verkleStateStore;
    private IVerkleDiffDb _keyValueStore;

    public ReadOnlyVerkleStateStore(VerkleStateStore verkleStateStore, IVerkleDiffDb keyValueStore)
    {
        _verkleStateStore = verkleStateStore;
        _keyValueStore = keyValueStore;
    }

    public byte[]? GetLeaf(byte[] key)
    {
        if (_keyValueStore.GetLeaf(key, out byte[]? value)) return value;
        return _verkleStateStore.GetLeaf(key);
    }
    public SuffixTree? GetStem(byte[] key)
    {
        if (_keyValueStore.GetStem(key, out var value)) return value;
        return _verkleStateStore.GetStem(key);
    }
    public InternalNode? GetBranch(byte[] key)
    {
        if (_keyValueStore.GetBranch(key, out var value)) return value;
        return _verkleStateStore.GetBranch(key);
    }
    public void SetLeaf(byte[] leafKey, byte[] leafValue)
    {
        _keyValueStore.SetLeaf(leafValue, leafValue);
    }
    public void SetStem(byte[] stemKey, SuffixTree suffixTree)
    {
        _keyValueStore.SetStem(stemKey, suffixTree);
    }
    public void SetBranch(byte[] branchKey, InternalNode internalNodeValue)
    {
        _keyValueStore.SetBranch(branchKey, internalNodeValue);
    }
    public void Flush(long blockNumber) { }

    public void ReverseState() { }
    public void ReverseState(IVerkleDiffDb reverseBatch, long numBlocks) { }

    public IVerkleDiffDb GetForwardMergedDiff(long fromBlock, long toBlock)
    {
        throw new NotImplementedException();
    }
    public IVerkleDiffDb GetReverseMergedDiff(long fromBlock, long toBlock)
    {
        throw new NotImplementedException();
    }
    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    public ReadOnlyVerkleStateStore AsReadOnly(IVerkleDiffDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(_verkleStateStore, keyValueStore);
    }
}
