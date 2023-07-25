// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Core.Verkle;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public partial class VerkleStateStore
{
    private bool _lastPersistedReachedReorgBoundary;
    private long _latestPersistedBlockNumber;
    public long LastPersistedBlockNumber
    {
        get => _latestPersistedBlockNumber;
        private set
        {
            if (value != _latestPersistedBlockNumber)
            {
                _latestPersistedBlockNumber = value;
                _lastPersistedReachedReorgBoundary = false;
            }
        }
    }
    private long LatestCommittedBlockNumber { get; set; }

    private Pedersen? PersistedStateRoot { get;  set; }

    // This method is called at the end of each block to flush the batch changes to the storage and generate forward and reverse diffs.
    // this should be called only once per block, right now it does not support multiple calls for the same block number.
    // if called multiple times, the full state would be fine - but it would corrupt the diffs and historical state will be lost
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber)
    //   is called multiple times for the same block number, but do we even need this?
    public void Flush(long blockNumber, VerkleMemoryDb batch)
    {
        if (_logger.IsDebug)
            _logger.Debug(
                $"VerkleStateStore - Flushing:{blockNumber} InternalDb:{batch.InternalTable.Count} LeafDb:{batch.LeafTable.Count}");

        if (blockNumber == 0)
        {
            PersistedStateRoot = GetStateRoot();
            LatestCommittedBlockNumber = LastPersistedBlockNumber = 0;
            PersistBlockChanges(batch.InternalTable, batch.LeafTable, Storage);
            StateRoot = GetStateRoot();
            _stateRootToBlocks[StateRoot] = blockNumber;
            if (_logger.IsDebug) _logger.Debug($"VerkleStateStore: Special case for block 0, StateRoot:{StateRoot}");
            return;
        }
        if (blockNumber <= LatestCommittedBlockNumber)
            throw new InvalidOperationException("Cannot flush for same block number `multiple times");

        ReadOnlyVerkleMemoryDb cacheBatch = new()
        {
            InternalTable = batch.InternalTable,
            LeafTable = new SortedDictionary<byte[], byte[]?>(batch.LeafTable, Bytes.Comparer)
        };

        bool persistBlock;
        ReadOnlyVerkleMemoryDb elemToPersist;
        long blockNumberPersist;
        if (BlockCache is null)
        {
            persistBlock = true;
            elemToPersist = cacheBatch;
            blockNumberPersist = blockNumber;
        }
        else
        {
            persistBlock = !BlockCache.EnqueueAndReplaceIfFull((blockNumber, cacheBatch),
                out (long, ReadOnlyVerkleMemoryDb) element);
            elemToPersist = element.Item2;
            blockNumberPersist = element.Item1;
        }

        if (persistBlock)
        {
            _logger.Info($"BlockCache is full - got forwardDiff BlockNumber:{blockNumberPersist} IN:{elemToPersist.InternalTable.Count} LN:{elemToPersist.LeafTable.Count}");
            Pedersen root = GetStateRoot(elemToPersist.InternalTable) ?? (new Pedersen(Storage.GetInternalNode(Array.Empty<byte>())?.InternalCommitment.Point.ToBytes().ToArray() ?? throw new ArgumentException()));
            PersistedStateRoot = root;
            _logger.Info($"StateRoot after persisting forwardDiff: {root}");
            VerkleMemoryDb reverseDiff = PersistBlockChanges(elemToPersist.InternalTable, elemToPersist.LeafTable, Storage);
            _logger.Info($"reverseDiff: IN:{reverseDiff.InternalTable.Count} LN:{reverseDiff.LeafTable.Count}");
            History?.InsertDiff(blockNumberPersist, elemToPersist, reverseDiff);
            LastPersistedBlockNumber =blockNumberPersist;
            Storage.LeafDb.Flush();
            Storage.InternalNodeDb.Flush();
        }

        LatestCommittedBlockNumber = blockNumber;
        StateRoot = GetStateRoot();
        _stateRootToBlocks[StateRoot] = blockNumber;
        _logger.Info(
            $"Completed Flush: PersistedStateRoot:{PersistedStateRoot} LastPersistedBlockNumber:{LastPersistedBlockNumber} LatestCommittedBlockNumber:{LatestCommittedBlockNumber} StateRoot:{StateRoot} blockNumber:{blockNumber}");
    }

    private VerkleMemoryDb PersistBlockChanges(IDictionary<byte[], InternalNode?> internalStore, IDictionary<byte[], byte[]?> leafStore, VerkleKeyValueDb storage)
    {
        if(_logger.IsDebug) _logger.Debug($"PersistBlockChanges: InternalStore:{internalStore.Count} LeafStore:{leafStore.Count}");
        // we should not have any null values in the Batch db - because deletion of values from verkle tree is not allowed
        // nullable values are allowed in MemoryStateDb only for reverse diffs.
        VerkleMemoryDb reverseDiff = new();

        foreach (KeyValuePair<byte[], byte[]?> entry in leafStore)
        {
            // in stateless tree - anything can be null
            // Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetLeaf(entry.Key, out byte[]? node)) reverseDiff.LeafTable[entry.Key] = node;
            else reverseDiff.LeafTable[entry.Key] = null;

            storage.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in internalStore)
        {
            // in stateless tree - anything can be null
            // Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetInternalNode(entry.Key, out InternalNode? node)) reverseDiff.InternalTable[entry.Key] = node;
            else reverseDiff.InternalTable[entry.Key] = null;

            storage.SetInternalNode(entry.Key, entry.Value);
        }

        if (_logger.IsDebug)
            _logger.Debug(
                $"PersistBlockChanges: ReverseDiff InternalStore:{reverseDiff.InternalTable.Count} LeafStore:{reverseDiff.LeafTable.Count}");

        return reverseDiff;
    }
}
