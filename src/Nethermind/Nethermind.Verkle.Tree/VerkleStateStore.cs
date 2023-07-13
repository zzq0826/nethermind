using System.Buffers.Binary;
using System.Diagnostics;
using DotNetty.Common.Utilities;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;
using LeafEnumerator = System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte[],byte[]>>;

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

    private StackQueue<(long, ReadOnlyVerkleMemoryDb)>? BlockCache { get; }

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
    private VerkleHistoryStore? History { get; }

    private readonly StateRootToBlockMap _stateRootToBlocks;

    public VerkleStateStore(IDbProvider dbProvider, ILogManager logManager, int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        History = new VerkleHistoryStore(dbProvider);
        _stateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }

    public VerkleStateStore(
        IDb leafDb,
        IDb internalDb,
        IDb forwardDiff,
        IDb reverseDiff,
        IDb stateRootToBlocks,
        ILogManager logManager,
        int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(internalDb, leafDb);
        History = new VerkleHistoryStore(forwardDiff, reverseDiff);
        _stateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }

    public VerkleStateStore(
        IDb leafDb,
        IDb internalDb,
        IDb stateRootToBlocks,
        ILogManager logManager,
        int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(internalDb, leafDb);
        _stateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new StackQueue<(long, ReadOnlyVerkleMemoryDb)>(maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(this, keyValueStore);
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

    public void Reset() => BlockCache?.Clear();

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        _logger.Info($"VerkleStateStore: init rootHash");
        InternalNode? node = GetInternalNode(Array.Empty<byte>());
        if (node is not null)
        {
            StateRoot = new Pedersen(node.InternalCommitment.Point.ToBytes());
            FullStatePersistedBlock = _stateRootToBlocks[StateRoot];
            FullStateCacheBlock = -1;
        }
        else
        {
            Storage.SetInternalNode(Array.Empty<byte>(), new InternalNode(VerkleNodeType.BranchNode));
            StateRoot = Pedersen.Zero;
            FullStatePersistedBlock = FullStateCacheBlock = -1;
        }

        // TODO: why should we store using block number - use stateRoot to index everything
        // but i think block number is easy to understand and it maintains a sequence
        if (FullStatePersistedBlock == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key)
    {
#if DEBUG
        if (key.Length != 32) throw new ArgumentException("key must be 32 bytes", nameof(key));
#endif
        if (BlockCache is not null)
        {
            using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
            while (diffs.MoveNext())
            {
                if (diffs.Current.Item2.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
            }
        }

        return Storage.GetLeaf(key, out byte[]? value) ? value : null;
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key)
    {
        if (BlockCache is not null)
        {
            using StackQueue<(long, ReadOnlyVerkleMemoryDb)>.StackEnumerator diffs = BlockCache.GetStackEnumerator();
            while (diffs.MoveNext())
            {
                if (diffs.Current.Item2.InternalTable.TryGetValue(key, out InternalNode? node)) return node;
            }
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
    // TODO: add capability to update the diffs instead of overwriting if Flush(long blockNumber)
    //   is called multiple times for the same block number, but do we even need this?
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
            FullStatePersistedBlock =blockNumberPersist;
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

        if (BlockCache is not null && BlockCache.Count != 0)
        {
            BlockCache.Pop(out _);
            return;
        }

        VerkleMemoryDb reverseDiff =
            History?.GetBatchDiff(FullStatePersistedBlock, FullStatePersistedBlock - 1).DiffLayer ??
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
        FullStateCacheBlock = toBlock;
        return true;
    }

    public IEnumerable<PathWithSubTree> GetLeafRangeIterator(Stem fromRange, Stem toRange, Pedersen stateRoot, long bytes)
    {
        if(bytes == 0)  yield break;

        long blockNumber = _stateRootToBlocks[stateRoot];
        byte[] fromRangeBytes = new byte[32];
        byte[] toRangeBytes = new byte[32];
        fromRange.BytesAsSpan.CopyTo(fromRangeBytes);
        toRange.BytesAsSpan.CopyTo(toRangeBytes);
        fromRangeBytes[31] = 0;
        toRangeBytes[31] = 255;

        using LeafEnumerator enumerator = GetLeafRangeIterator(fromRangeBytes, toRangeBytes, blockNumber).GetEnumerator();

        int usedBytes = 0;

        HashSet<Stem> listOfStem = new();
        Stem currentStem = fromRange;
        List<LeafInSubTree> subTree = new(256);

        while (enumerator.MoveNext())
        {
            KeyValuePair<byte[], byte[]> current = enumerator.Current;
            if (listOfStem.Contains(current.Key.Slice(0,31)))
            {
                subTree.Add(new LeafInSubTree(current.Key[31], current.Value));
                usedBytes += 31;
            }
            else
            {
                if (subTree.Count != 0) yield return new PathWithSubTree(currentStem, subTree.ToArray());
                if (usedBytes >= bytes) break;
                subTree.Clear();
                currentStem = new Stem(current.Key.Slice(0,31).ToArray());
                listOfStem.Add(currentStem);
                subTree.Add(new LeafInSubTree(current.Key[31], current.Value));
                usedBytes += 31 + 33;
            }
        }
        if (subTree.Count != 0) yield return new PathWithSubTree(currentStem, subTree.ToArray());
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange, byte[] toRange, long blockNumber)
    {
        if(BlockCache is null) yield break;

        // this will contain all the iterators that we need to fulfill the GetSubTreeRange request
        List<LeafEnumerator> iterators = new();

        // kvMap is used to keep a map of keyValues we encounter - this is for ease of access - but not optimal
        // TODO: remove this - merge kvMap and kvEnumMap
        Dictionary<byte[], KeyValuePair<int, byte[]>> kvMap = new(Bytes.EqualityComparer);
        // this created a sorted structure for all the keys and the corresponding enumerators. the idea is that get
        // the first key (sorted), remove the key, then move the enumerator to next and insert the new key and
        // enumerator again
        DictionarySortedSet<byte[], LeafIterator> keyEnumMap = new(Bytes.Comparer);

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

                // find the first value in iterator that is not already used
                bool isIteratorUsed = false;
                while (enumerator.MoveNext())
                {
                    KeyValuePair<byte[], byte[]> current = enumerator.Current;
                    // add the key and corresponding value
                    if (kvMap.TryAdd(current.Key, new(iteratorPriority, current.Value)))
                    {
                        isIteratorUsed = true;
                        iterators.Add(enumerator);
                        // add the new key and the corresponding enumerator
                        keyEnumMap.Add(current.Key, new(enumerator, iteratorPriority));
                        break;
                    }
                }
                if (!isIteratorUsed)
                {
                    enumerator.Dispose();
                    continue;
                }
                iteratorPriority++;
            }

            LeafEnumerator persistentLeafsIterator = Storage.LeafDb.GetIterator(fromRange, toRange).GetEnumerator();
            bool isPersistentIteratorUsed = false;
            while (persistentLeafsIterator.MoveNext())
            {
                KeyValuePair<byte[], byte[]> current = persistentLeafsIterator.Current;
                // add the key and corresponding value
                if (kvMap.TryAdd(current.Key, new(iteratorPriority, current.Value)))
                {
                    isPersistentIteratorUsed = true;
                    iterators.Add(persistentLeafsIterator);
                    // add the new key and the corresponding enumerator
                    keyEnumMap.Add(current.Key, new (persistentLeafsIterator, iteratorPriority));
                    break;
                }
            }
            if (!isPersistentIteratorUsed)
            {
                persistentLeafsIterator.Dispose();
            }

            void InsertAndMoveIteratorRecursive(LeafIterator leafIterator)
            {
                while (leafIterator.Enumerator.MoveNext())
                {
                    KeyValuePair<byte[], byte[]> newKeyValuePair = leafIterator.Enumerator.Current;
                    byte[] newKeyToInsert = newKeyValuePair.Key;
                    // now here check if the value already exist and if the priority of value of higher or lower and
                    // update accordingly
                    KeyValuePair<int, byte[]> valueToInsert = new(leafIterator.Priority, newKeyValuePair.Value);

                    if (kvMap.TryGetValue(newKeyToInsert, out KeyValuePair<int, byte[]> valueExisting))
                    {
                        // priority of the new value is smaller (more) than the priority of old value
                        if (valueToInsert.Key < valueExisting.Key)
                        {
                            keyEnumMap.TryGetValue(newKeyValuePair.Key, out LeafIterator? prevIterator);
                            keyEnumMap.Remove(newKeyValuePair.Key);

                            // replace the existing value
                            keyEnumMap.Add(newKeyValuePair.Key, leafIterator);
                            kvMap[newKeyValuePair.Key] = valueToInsert;

                            // since we replacing the existing value, we need to move the prevIterator iterator to
                            // next value till we get the new value
                            InsertAndMoveIteratorRecursive(prevIterator);
                            break;
                        }

                        // since we were not able to add current value from this iterator, move to next value and try
                        // to add that
                    }
                    else
                    {
                        // this is the most simple case
                        // since there was no existing value - we just insert without modifying other iterators
                        keyEnumMap.Add(newKeyValuePair.Key, leafIterator);
                        kvMap.Add(newKeyValuePair.Key, valueToInsert);
                        break;
                    }
                }
            }

            while (keyEnumMap.Count > 0)
            {
                // get the first value from the sorted set
                KeyValuePair<byte[], LeafIterator> value = keyEnumMap.Min;
                // remove the corresponding element because it will be used
                keyEnumMap.Remove(value.Key);

                // get the enumerator and move it to next and insert the corresponding values recursively
                InsertAndMoveIteratorRecursive(value.Value);

                byte[] returnValue = kvMap[value.Key].Value;
                kvMap.Remove(value.Key);

                // return the value
                yield return new KeyValuePair<byte[], byte[]> (value.Key, returnValue);
            }
        }
        finally
        {
            foreach (LeafEnumerator t in iterators) t.Dispose();
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

    public List<PathWithSubTree>? GetLeafRangeIterator(byte[] fromRange, byte[] toRange, Pedersen stateRoot, long bytes)
    {
        long blockNumber = _stateRootToBlocks[stateRoot];
        using IEnumerator<KeyValuePair<byte[], byte[]>> ranges = GetLeafRangeIterator(fromRange, toRange, blockNumber).GetEnumerator();

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
