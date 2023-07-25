using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.VerkleDb;
using LeafEnumerator = System.Collections.Generic.IEnumerator<System.Collections.Generic.KeyValuePair<byte[],byte[]>>;

namespace Nethermind.Verkle.Tree;

public partial class VerkleStateStore : IVerkleStore, ISyncTrieStore
{
    private readonly ILogger _logger;

    public Pedersen RootHash
    {
        get => GetStateRoot();
        set => MoveToStateRoot(value);
    }

    public Pedersen StateRoot { get; private set; }
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

    // The underlying key value database
    // We try to avoid fetching from this, and we only store at the end of a batch insert
    private VerkleKeyValueDb Storage { get; }

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

    public void Reset() => BlockCache?.Clear();

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        InternalNode? node = GetInternalNode(Array.Empty<byte>());
        if (node is not null)
        {
            StateRoot = new Pedersen(node.InternalCommitment.Point.ToBytes());
            LastPersistedBlockNumber = _stateRootToBlocks[StateRoot];
            LatestCommittedBlockNumber = -1;
        }
        else
        {
            Storage.SetInternalNode(Array.Empty<byte>(), new InternalNode(VerkleNodeType.BranchNode));
            StateRoot = Pedersen.Zero;
            LastPersistedBlockNumber = LatestCommittedBlockNumber = -1;
        }

        // TODO: why should we store using block number - use stateRoot to index everything
        // but i think block number is easy to understand and it maintains a sequence
        if (LastPersistedBlockNumber == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
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

    private int _isFirst;
    private void AnnounceReorgBoundaries()
    {
        if (LatestCommittedBlockNumber < 1)
        {
            return;
        }

        bool shouldAnnounceReorgBoundary = false;
        bool isFirstCommit = Interlocked.Exchange(ref _isFirst, 1) == 0;
        if (isFirstCommit)
        {
            if (_logger.IsDebug) _logger.Debug($"Reached first commit - newest {LatestCommittedBlockNumber}, last persisted {LastPersistedBlockNumber}");
            // this is important when transitioning from fast sync
            // imagine that we transition at block 1200000
            // and then we close the app at 1200010
            // in such case we would try to continue at Head - 1200010
            // because head is loaded if there is no persistence checkpoint
            // so we need to force the persistence checkpoint
            long baseBlock = Math.Max(0, LatestCommittedBlockNumber - 1);
            LastPersistedBlockNumber = baseBlock;
            shouldAnnounceReorgBoundary = true;
        }
        else if (!_lastPersistedReachedReorgBoundary)
        {
            // even after we persist a block we do not really remember it as a safe checkpoint
            // until max reorgs blocks after
            if (LatestCommittedBlockNumber >= LastPersistedBlockNumber + MaxNumberOfBlocksInCache)
            {
                shouldAnnounceReorgBoundary = true;
            }
        }

        if (shouldAnnounceReorgBoundary)
        {
            ReorgBoundaryReached?.Invoke(this, new ReorgBoundaryReached(LastPersistedBlockNumber));
            _lastPersistedReachedReorgBoundary = true;
        }
    }
}
