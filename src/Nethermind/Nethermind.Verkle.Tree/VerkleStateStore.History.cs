// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public partial class VerkleStateStore
{
    public event EventHandler<InsertBatchCompleted>? InsertBatchCompleted;
    /// <summary>
    ///  maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int MaxNumberOfBlocksInCache { get; }
    private BlockDiffCache? BlockCache { get; }

    private VerkleHistoryStore? History { get; }

    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        if (changeSet.FromBlockNumber != LastPersistedBlockNumber)
        {
            throw new ArgumentException(
                $"This case should not be possible. Diff fromBlock should be equal to persisted block number. FullStateBlock:{LastPersistedBlockNumber}!=fromBlock:{changeSet.FromBlockNumber}",
                nameof(changeSet.FromBlockNumber));
        }


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

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock < toBlock - move forward in time
    public bool GetForwardMergedDiff(long fromBlock, long toBlock, [MaybeNullWhen(false)]out VerkleMemoryDb diff)
    {
        if (History is null)
        {
            diff = default;
            return false;
        }
        diff = History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
        return true;
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock > toBlock - move back in time
    public bool GetReverseMergedDiff(long fromBlock, long toBlock, [MaybeNullWhen(false)]out VerkleMemoryDb diff)
    {
        if (History is null)
        {
            diff = default;
            return false;
        }
        diff = History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
        return true;
    }

    private readonly StateRootToBlockMap StateRootToBlocks;

    private readonly struct StateRootToBlockMap
    {
        private readonly IDb _stateRootToBlock;

        public StateRootToBlockMap(IDb stateRootToBlock)
        {
            _stateRootToBlock = stateRootToBlock;
        }

        public long this[VerkleCommitment key]
        {
            get
            {
                // if (Pedersen.Zero.Equals(key)) return -1;
                byte[]? encodedBlock = _stateRootToBlock[key.Bytes];
                return encodedBlock is null ? -2 : BinaryPrimitives.ReadInt64LittleEndian(encodedBlock);
            }
            set
            {
                Span<byte> encodedBlock = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(encodedBlock, value);
                if(!_stateRootToBlock.KeyExists(key.Bytes))
                    _stateRootToBlock.Set(key.Bytes, encodedBlock.ToArray());
            }
        }
    }
}
