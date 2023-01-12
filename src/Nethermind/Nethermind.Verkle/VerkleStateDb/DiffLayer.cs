// Copyright 2022 Demerzel Solutions Limited
// Licensed under Apache-2.0. For full terms, see LICENSE in the project root.

using System.Diagnostics;
using Nethermind.Db;
using Nethermind.Verkle.VerkleNodes;

namespace Nethermind.Verkle.VerkleStateDb;

public enum DiffType
{
    Forward,
    Reverse
}

public class DiffLayer : IDiffLayer
{
    private readonly DiffType _diffType;
    private IDb DiffDb { get; }
    public DiffLayer(IDb diffDb, DiffType diffType)
    {
        DiffDb = diffDb;
        _diffType = diffType;
    }
    public void InsertDiff(long blockNumber, IVerkleMemoryDb memory)
    {
        DiffDb.Set(blockNumber, memory.Encode());
    }
    public IVerkleMemoryDb FetchDiff(long blockNumber)
    {
        byte[]? diff = DiffDb.Get(blockNumber);
        if (diff is null) throw new ArgumentException(null, nameof(blockNumber));
        return MemoryStateDb.Decode(diff);
    }

    public IVerkleMemoryDb MergeDiffs(long fromBlock, long toBlock)
    {
        MemoryStateDb mergedDiff = new MemoryStateDb();
        switch (_diffType)
        {
            case DiffType.Reverse:
                Debug.Assert(fromBlock > toBlock);
                for (long i = toBlock; i <= fromBlock; i++)
                {
                    IVerkleMemoryDb reverseDiff = FetchDiff(i);
                    foreach (KeyValuePair<byte[], byte[]?> item in reverseDiff.LeafTable)
                    {
                        mergedDiff.LeafTable.TryAdd(item.Key, item.Value);
                    }
                    foreach (KeyValuePair<byte[], InternalNode?> item in reverseDiff.BranchTable)
                    {
                        mergedDiff.BranchTable.TryAdd(item.Key, item.Value);
                    }
                    foreach (KeyValuePair<byte[], SuffixTree?> item in reverseDiff.StemTable)
                    {
                        mergedDiff.StemTable.TryAdd(item.Key, item.Value);
                    }
                }
                break;
            case DiffType.Forward:
                Debug.Assert(fromBlock < toBlock);
                for (long i = toBlock; i >= fromBlock; i--)
                {
                    IVerkleMemoryDb forwardDiff = FetchDiff(i);
                    foreach (KeyValuePair<byte[], byte[]?> item in forwardDiff.LeafTable)
                    {
                        mergedDiff.LeafTable.TryAdd(item.Key, item.Value);
                    }
                    foreach (KeyValuePair<byte[], InternalNode?> item in forwardDiff.BranchTable)
                    {
                        mergedDiff.BranchTable.TryAdd(item.Key, item.Value);
                    }
                    foreach (KeyValuePair<byte[], SuffixTree?> item in forwardDiff.StemTable)
                    {
                        mergedDiff.StemTable.TryAdd(item.Key, item.Value);
                    }
                }
                break;
            default:
                throw new NotSupportedException();
        }
        return mergedDiff;
    }
}
