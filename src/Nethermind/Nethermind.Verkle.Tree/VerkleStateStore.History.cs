// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public partial class VerkleStateStore
{
    private readonly StateRootToBlockMap _stateRootToBlocks;

    /// <summary>
    ///  maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int MaxNumberOfBlocksInCache { get; set; }

    private StackQueue<(long, ReadOnlyVerkleMemoryDb)>? BlockCache { get; }

    private VerkleHistoryStore? History { get; }


    // now the full state back in time by one block.
    public void ReverseState()
    {

        if (BlockCache is not null && BlockCache.Count != 0)
        {
            BlockCache.Pop(out _);
            return;
        }

        VerkleMemoryDb reverseDiff =
            History?.GetBatchDiff(LastPersistedBlockNumber, LastPersistedBlockNumber - 1).DiffLayer ??
            throw new ArgumentException("History not Enabled");

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out byte[]? node);
            if (node is null)
            {
                Storage.RemoveLeaf(entry.Key);
            }
            else
            {
                Storage.SetLeaf(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.InternalTable)
        {
            reverseDiff.GetInternalNode(entry.Key, out InternalNode? node);
            if (node is null)
            {
                Storage.RemoveInternalNode(entry.Key);
            }
            else
            {
                Storage.SetInternalNode(entry.Key, node);
            }
        }
        LastPersistedBlockNumber -= 1;
    }

    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        if (changeSet.FromBlockNumber != LastPersistedBlockNumber)
            throw new ArgumentException($"Cannot apply diff FullStateBlock:{LastPersistedBlockNumber}!=fromBlock:{changeSet.FromBlockNumber}", nameof(changeSet.FromBlockNumber));

        VerkleMemoryDb reverseDiff = changeSet.DiffLayer;

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out byte[]? node);
            if (node is null)
            {
                Storage.RemoveLeaf(entry.Key);
            }
            else
            {
                Storage.SetLeaf(entry.Key, node);
            }
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.InternalTable)
        {
            reverseDiff.GetInternalNode(entry.Key, out InternalNode? node);
            if (node is null)
            {
                Storage.RemoveInternalNode(entry.Key);
            }
            else
            {
                Storage.SetInternalNode(entry.Key, node);
            }
        }
        LastPersistedBlockNumber = changeSet.ToBlockNumber;
    }

    public bool MoveToStateRoot(Pedersen stateRoot)
    {
        Pedersen currentRoot = GetStateRoot();
        _logger.Info($"VerkleStateStore - MoveToStateRoot: WantedStateRoot: {stateRoot} CurrentStateRoot: {currentRoot}");
        // TODO: this is actually not possible - not sure if return true is correct here
        if (stateRoot.Equals(new Pedersen(Keccak.EmptyTreeHash.Bytes))) return true;

        long fromBlock = _stateRootToBlocks[currentRoot];
        if(fromBlock == -1) return false;
        long toBlock = _stateRootToBlocks[stateRoot];
        if (toBlock == -1) return false;

        _logger.Info($"VerkleStateStore - MoveToStateRoot: fromBlock: {fromBlock} toBlock: {toBlock}");

        if (fromBlock > toBlock)
        {
            long noOfBlockToMove = fromBlock - toBlock;
            if (BlockCache is not null && noOfBlockToMove > BlockCache.Count)
            {
                BlockCache.Clear();
                fromBlock -= BlockCache.Count;

                BatchChangeSet batchDiff = History?.GetBatchDiff(fromBlock, toBlock) ??
                                           throw new ArgumentException("History not Enabled");
                ApplyDiffLayer(batchDiff);

            }
            else
            {
                if (BlockCache is not null)
                {
                    for (int i = 0; i < noOfBlockToMove; i++)
                    {
                        BlockCache.Pop(out _);
                    }
                }
            }
        }
        else if (fromBlock == toBlock)
        {

        }
        else
        {
            throw new NotImplementedException("Should be implemented in future (probably)");
        }

        Debug.Assert(GetStateRoot().Equals(stateRoot));
        LatestCommittedBlockNumber = toBlock;
        return true;
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock < toBlock - move forward in time
    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock)
    {
        return History?.GetBatchDiff(fromBlock, toBlock).DiffLayer ?? throw new ArgumentException("History not Enabled");
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock > toBlock - move back in time
    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock)
    {
        return History?.GetBatchDiff(fromBlock, toBlock).DiffLayer ?? throw new ArgumentException("History not Enabled");
    }
}
