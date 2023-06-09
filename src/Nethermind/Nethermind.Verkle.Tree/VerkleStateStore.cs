using System.Buffers.Binary;
using System.Diagnostics;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public class VerkleStateStore : IVerkleStore, ISyncTrieStore
{
    /// <summary>
    ///  maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int MaxNumberOfBlocksInCache { get; set; }

    /// <summary>
    /// the blockNumber for with the fullState is persisted in the database.
    /// </summary>
    private long FullStatePersistedBlock { get; set; }
    private long FullStateCacheBlock { get; set; }

    private Pedersen? PersistedStateRoot { get;  set; }

    private StackQueue<(long, ReadOnlyVerkleMemoryDb)> BlockCache { get; set; }

    public Pedersen StateRoot { get; private set; }

    private readonly ILogger _logger;

    public Pedersen RootHash
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }


    // The underlying key value database
    // We try to avoid fetching from this, and we only store at the end of a batch insert
    private VerkleKeyValueDb Storage { get; }

    private VerkleHistoryStore History { get; }

    private readonly StateRootToBlockMap _stateRootToBlocks;

    public VerkleStateStore(IDbProvider dbProvider, ILogManager logManager, int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        History = new VerkleHistoryStore(dbProvider);
        _stateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
        StateRoot = GetStateRoot();
        FullStatePersistedBlock = _stateRootToBlocks[StateRoot];
        FullStateCacheBlock = -1;

        // TODO: why should we store using block number - use stateRoot to index everything
        // but i think block number is easy to understand and it maintains a sequence
        if (FullStatePersistedBlock == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
    }

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(this, keyValueStore);
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock < toBlock - move forward in time
    public VerkleMemoryDb GetForwardMergedDiff(long fromBlock, long toBlock)
    {
        return History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock > toBlock - move back in time
    public VerkleMemoryDb GetReverseMergedDiff(long fromBlock, long toBlock)
    {
        return History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
    }

    public void Reset()
    {
        BlockCache.Clear();
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        _logger.Info($"VerkleStateStore: init rootHash");
        InternalNode? node = GetInternalNode(Array.Empty<byte>());
        if (node is not null) return;
        Storage.SetInternalNode(Array.Empty<byte>(), new InternalNode(VerkleNodeType.BranchNode));
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key)
    {
#if DEBUG
        if (key.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(key));
#endif
        using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
        while (diffs.MoveNext())
        {
            if (diffs.Current.Item2.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
        }

        return Storage.GetLeaf(key, out byte[]? value) ? value : null;
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key)
    {
        using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
        while (diffs.MoveNext())
        {
            if (diffs.Current.Item2.InternalTable.TryGetValue(key, out InternalNode? node)) return node;
        }
        return Storage.GetInternalNode(key, out InternalNode? value) ? value : null;
    }

    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
#if DEBUG
        if (leafKey.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(leafKey));
        if (leafValue.Length != 32) throw new ArgumentException("value must be 32 bytes", nameof(leafValue));
#endif
        Storage.SetLeaf(leafKey, leafValue);
    }

    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        Storage.SetInternalNode(internalNodeKey, internalNodeValue);
    }

    // This method is called at the end of each block to flush the batch changes to the storage and generate forward and reverse diffs.
    // this should be called only once per block, right now it does not support multiple calls for the same block number.
    // if called multiple times, the full state would be fine - but it would corrupt the diffs and historical state will be lost
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber) is called multiple times for the same block number
    public void Flush(long blockNumber, VerkleMemoryDb batch)
    {
        if (_logger.IsDebug)
            _logger.Debug(
                $"VerkleStateStore - Flushing: {blockNumber} InternalDb:{batch.InternalTable.Count} LeafDb:{batch.LeafTable.Count}");

        if (blockNumber == 0)
        {
            PersistedStateRoot = GetStateRoot();
            FullStateCacheBlock = FullStatePersistedBlock = 0;
            PersistBlockChanges(batch.InternalTable, batch.LeafTable, Storage);
            StateRoot = GetStateRoot();
            _stateRootToBlocks[StateRoot] = blockNumber;
            _logger.Info($"VerkleStateStore: Special case for block 0, StateRoot:{StateRoot}");
            return;
        }
        if (blockNumber <= FullStateCacheBlock)
            throw new InvalidOperationException("Cannot flush for same block number `multiple times");

        ReadOnlyVerkleMemoryDb cacheBatch = new()
        {
            InternalTable = batch.InternalTable,
            LeafTable = new SortedDictionary<byte[], byte[]?>(batch.LeafTable, Bytes.Comparer)
        };

        if (!BlockCache.EnqueueAndReplaceIfFull((blockNumber, cacheBatch), out (long, ReadOnlyVerkleMemoryDb) element))
        {
            _logger.Info($"BlockCache is full - got forwardDiff BlockNumber:{element.Item1} IN:{element.Item2.InternalTable.Count} LN:{element.Item2.LeafTable.Count}");
            Pedersen root = GetStateRoot(element.Item2.InternalTable) ?? (new Pedersen(Storage.GetInternalNode(Array.Empty<byte>())?.InternalCommitment.Point.ToBytes().ToArray() ?? throw new ArgumentException()));
            PersistedStateRoot = root;
            _logger.Info($"StateRoot after persisting forwardDiff: {root}");
            VerkleMemoryDb reverseDiff = PersistBlockChanges(element.Item2.InternalTable, element.Item2.LeafTable, Storage);
            _logger.Info($"reverseDiff: IN:{reverseDiff.InternalTable.Count} LN:{reverseDiff.LeafTable.Count}");
            History.InsertDiff(element.Item1, element.Item2, reverseDiff);
            FullStatePersistedBlock = element.Item1;
            Storage.LeafDb.Flush();
            Storage.InternalNodeDb.Flush();
        }

        FullStateCacheBlock = blockNumber;
        StateRoot = GetStateRoot();
        _stateRootToBlocks[StateRoot] = blockNumber;
        _logger.Info(
            $"Completed Flush: PersistedStateRoot:{PersistedStateRoot} FullStatePersistedBlock:{FullStatePersistedBlock} FullStateCacheBlock:{FullStateCacheBlock} StateRoot:{StateRoot} blockNumber:{blockNumber}");
    }

    // now the full state back in time by one block.
    public void ReverseState()
    {

        if (BlockCache.Count != 0)
        {
            BlockCache.Pop(out _);
            return;
        }

        VerkleMemoryDb reverseDiff = History.GetBatchDiff(FullStatePersistedBlock, FullStatePersistedBlock - 1).DiffLayer;

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
        FullStatePersistedBlock -= 1;
    }

    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        if (changeSet.FromBlockNumber != FullStatePersistedBlock)
            throw new ArgumentException($"Cannot apply diff FullStateBlock:{FullStatePersistedBlock}!=fromBlock:{changeSet.FromBlockNumber}", nameof(changeSet.FromBlockNumber));

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
        FullStatePersistedBlock = changeSet.ToBlockNumber;
    }

    public bool IsFullySynced(Keccak stateRoot) => _stateRootToBlocks[new Pedersen(stateRoot.Bytes)] != -2;

    public Pedersen GetStateRoot()
    {
        byte[] stateRoot = GetInternalNode(Array.Empty<byte>())?.InternalCommitment.Point.ToBytes().ToArray() ??
                           throw new InvalidOperationException();
        return new Pedersen(stateRoot);
    }

    private static Pedersen? GetStateRoot(IVerkleDb db)
    {
        return db.GetInternalNode(Array.Empty<byte>(), out InternalNode? node) ? new Pedersen(node!.InternalCommitment.Point.ToBytes().ToArray()) : null;
    }

    private static Pedersen? GetStateRoot(InternalStore db)
    {
        return db.TryGetValue(Array.Empty<byte>(), out InternalNode? node) ? new Pedersen(node!.InternalCommitment.Point.ToBytes().ToArray()) : null;
    }

    public bool MoveToStateRoot(Pedersen stateRoot)
    {
        Pedersen currentRoot = GetStateRoot();
        _logger.Info($"VerkleStateStore - MoveToStateRoot: WantedStateRoot:{stateRoot} CurrentStateRoot:{currentRoot}");
        // if the target root node is same as current - return true
        if (currentRoot.Equals(stateRoot)) return true;
        // TODO: this is actually not possible - not sure if return true is correct here
        if (stateRoot.Equals(new Pedersen(Keccak.EmptyTreeHash.Bytes))) return true;

        long fromBlock = _stateRootToBlocks[currentRoot];
        if(fromBlock == -1) return false;
        long toBlock = _stateRootToBlocks[stateRoot];
        if (toBlock == -1) return false;

        _logger.Info($"VerkleStateStore - MoveToStateRoot: fromBlock:{fromBlock} toBlock:{toBlock}");

        if (fromBlock > toBlock)
        {
            long noOfBlockToMove = fromBlock - toBlock;
            if (noOfBlockToMove > BlockCache.Count)
            {
                BlockCache.Clear();
                fromBlock -= BlockCache.Count;

                BatchChangeSet batchDiff = History.GetBatchDiff(fromBlock, toBlock);
                ApplyDiffLayer(batchDiff);

            }
            else
            {
                for (int i = 0; i < noOfBlockToMove; i++)
                {
                    BlockCache.Pop(out _);
                }
            }
        }
        else
        {
            throw new NotImplementedException("Should be implemented in future (probably)");
        }

        Debug.Assert(GetStateRoot().Equals(stateRoot));
        return true;
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange, byte[] toRange, long blockNumber)
    {
        // this will contain all the iterators that we need to fulfill the GetSubTreeRange request
        List<IEnumerator<KeyValuePair<byte[], byte[]>>> iterators = new();

        // kvMap is used to keep a map of keyValues we encounter - this is for ease of access - but not optimal
        // TODO: remove this - merge kvMap and kvEnumMap
        Dictionary<byte[], byte[]> kvMap = new (Bytes.EqualityComparer);
        // this created a sorted structure for all the keys and the corresponding enumerators. the idea is that get
        // the first key (sorted), remove the key, then move the enumerator to next and insert the new key and
        // enumerator again
        DictionarySortedSet<byte[], IEnumerator<KeyValuePair<byte[], byte[]>>> keyEnumMap = new(Bytes.Comparer);

        // TODO: optimize this to start from a specific blockNumber - or better yet get the list of enumerators directly
        using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator blockEnumerator =
            BlockCache.GetStackEnumerator();
        try
        {
            int iteratorPriority = 0;
            while (blockEnumerator.MoveNext())
            {
                // enumerate till we get to the required block number
                if(blockEnumerator.Current.Item1 > blockNumber) continue;

                // TODO: here we construct a set from the LeafTable so that we can do the GetViewBetween
                //   obviously this is very un-optimal but the idea is to replace the LeafTable with SortedSet in the
                //   blockCache itself. The reason we want to use GetViewBetween because this is optimal to do seek
                DictionarySortedSet<byte[], byte[]> currentSet = new (blockEnumerator.Current.Item2.LeafTable, Bytes.Comparer);

                // construct the iterators that starts for the specific range using GetViewBetween
                IEnumerator<KeyValuePair<byte[],byte[]>> enumerator = currentSet
                    .GetViewBetween(
                        new KeyValuePair<byte[], byte[]>(fromRange, Pedersen.Zero.Bytes),
                        new KeyValuePair<byte[], byte[]>(toRange, Pedersen.Zero.Bytes))
                    .GetEnumerator();

                if (!enumerator.MoveNext())
                {
                    enumerator.Dispose();
                    continue;
                }
                iterators.Add(enumerator);

                KeyValuePair<byte[], byte[]> current = enumerator.Current;
                // add the new key and the corresponding enumerator
                keyEnumMap.Add(current.Key, enumerator);
                // add the key and corresponding value
                kvMap.Add(current.Key, current.Value);
            }

            IEnumerator<KeyValuePair<byte[], byte[]>> kvEnum = Storage.LeafDb.GetIterator(fromRange, toRange).GetEnumerator();
            if (!kvEnum.MoveNext())
            {
                kvEnum.Dispose();
            }
            else
            {
                iterators.Add(kvEnum);

                KeyValuePair<byte[], byte[]> kvCurrent = kvEnum.Current;
                // add the new key and the corresponding enumerator
                keyEnumMap.Add(kvCurrent.Key, kvEnum);
                // add the key and corresponding value
                kvMap.Add(kvCurrent.Key, kvCurrent.Value);
            }

            while (keyEnumMap.Count > 0)
            {

                // get the first value from the sorted set
                KeyValuePair<byte[], IEnumerator<KeyValuePair<byte[], byte[]>>> value = keyEnumMap.Min;
                // remove the corresponding element because it will be used
                keyEnumMap.Remove(value.Key);

                // get the enumerator and move it to next and insert he corresponding values
                IEnumerator<KeyValuePair<byte[], byte[]>> enumerator = value.Value;
                if (enumerator.MoveNext())
                {
                    KeyValuePair<byte[], byte[]> current = enumerator.Current;
                    keyEnumMap.Add(current.Key, enumerator);
                    kvMap.Add(current.Key, current.Value);
                }

                // return the value
                yield return new KeyValuePair<byte[], byte[]> (value.Key, kvMap[value.Key]!);
            }
        }
        finally
        {
            foreach (IEnumerator<KeyValuePair<byte[], byte[]>> t in iterators) t.Dispose();
        }
    }

    private VerkleMemoryDb PersistBlockChanges(IDictionary<byte[], InternalNode?> internalStore, IDictionary<byte[], byte[]?> leafStore, VerkleKeyValueDb storage)
    {
        if(_logger.IsDebug) _logger.Debug($"PersistBlockChanges: InternalStore:{internalStore.Count} LeafStore:{leafStore.Count}");
        // we should not have any null values in the Batch db - because deletion of values from verkle tree is not allowed
        // nullable values are allowed in MemoryStateDb only for reverse diffs.
        VerkleMemoryDb reverseDiff = new();

        foreach (KeyValuePair<byte[], byte[]?> entry in leafStore)
        {
            Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetLeaf(entry.Key, out byte[]? node)) reverseDiff.LeafTable[entry.Key] = node;
            else reverseDiff.LeafTable[entry.Key] = null;

            storage.SetLeaf(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in internalStore)
        {
            Debug.Assert(entry.Value is not null, "nullable value only for reverse diff");
            if (storage.GetInternalNode(entry.Key, out InternalNode? node)) reverseDiff.InternalTable[entry.Key] = node;
            else reverseDiff.InternalTable[entry.Key] = null;

            if(entry.Value.ShouldPersist) storage.SetInternalNode(entry.Key, entry.Value);
        }

        if (_logger.IsDebug)
            _logger.Debug(
                $"PersistBlockChanges: ReverseDiff InternalStore:{reverseDiff.InternalTable.Count} LeafStore:{reverseDiff.LeafTable.Count}");

        return reverseDiff;
    }

    private readonly struct StateRootToBlockMap
    {
        private readonly IDb _stateRootToBlock;

        public StateRootToBlockMap(IDb stateRootToBlock)
        {
            _stateRootToBlock = stateRootToBlock;
        }

        public long this[Pedersen key]
        {
            get
            {
                if (Pedersen.Zero.Equals(key)) return -1;
                byte[]? encodedBlock = _stateRootToBlock[key.Bytes];
                return encodedBlock is null ? -2 : BinaryPrimitives.ReadInt64LittleEndian(encodedBlock);
            }
            set
            {
                Span<byte> encodedBlock = stackalloc byte[8];
                BinaryPrimitives.WriteInt64LittleEndian(encodedBlock, value);
                _stateRootToBlock.Set(key.Bytes, encodedBlock.ToArray());
            }
        }
    }

    public List<PathWithSubTree>? GetLeafRangeIterator(byte[] fromRange, byte[] toRange, Pedersen stateRoot, long bytes)
    {
        long blockNumber = _stateRootToBlocks[stateRoot];
        using IEnumerator<KeyValuePair<byte[], byte[]?>> ranges = GetLeafRangeIterator(fromRange, toRange, blockNumber).GetEnumerator();

        long currentBytes = 0;

        SpanDictionary<byte, List<LeafInSubTree>> rangesToReturn = new(Bytes.SpanEqualityComparer);

        if (!ranges.MoveNext()) return null;

        // handle the first element
        Span<byte> stem = ranges.Current.Key.AsSpan()[..31];
        rangesToReturn.TryAdd(stem, new List<LeafInSubTree>());
        rangesToReturn[stem].Add(new LeafInSubTree(ranges.Current.Key[31], ranges.Current.Value!));
        currentBytes += 64;


        bool bytesConsumed = false;
        while (ranges.MoveNext())
        {
            if (currentBytes > bytes)
            {
                bytesConsumed = true;
                break;
            }
        }

        if (bytesConsumed)
        {
            // this means the iterator is not empty but the bytes is consumed, now we need to complete the current
            // subtree we are processing
            while (ranges.MoveNext())
            {
                // if stem is present that means we have to complete that subTree
                stem = ranges.Current.Key.AsSpan()[..31];
                if (rangesToReturn.TryGetValue(stem, out List<LeafInSubTree>? listOfLeafs))
                {
                    listOfLeafs.Add(new LeafInSubTree(ranges.Current.Key[31], ranges.Current.Value!));
                    continue;
                }
                break;
            }
        }

        List<PathWithSubTree> pathWithSubTrees = new(rangesToReturn.Count);
        foreach (KeyValuePair<byte[], List<LeafInSubTree>> keyVal in rangesToReturn)
        {
            pathWithSubTrees.Add(new PathWithSubTree(keyVal.Key, keyVal.Value.ToArray()));
        }

        return pathWithSubTrees;
    }
}
