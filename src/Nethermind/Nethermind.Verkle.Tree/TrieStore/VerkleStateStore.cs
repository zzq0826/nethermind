using System;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Cache;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public partial class VerkleStateStore : IVerkleTrieStore, ISyncTrieStore
{
    private readonly ILogger _logger;
    public readonly ILogManager LogManager;

    /// <summary>
    /// </summary>
    public readonly StateRootToBlockMap StateRootToBlocks;


    /// <summary>
    ///     Keep track of LastPersistedBlock and if the LastPersistedBlock reached the reorg boundary and was marked safe
    /// </summary>
    private bool _lastPersistedReachedReorgBoundary;

    private long _latestPersistedBlockNumber;


    public VerkleStateStore(IDbProvider dbProvider, int blockCacheSize, ILogManager logManager)
    {
        LogManager = logManager;
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        StateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = new BlockBranchCache(blockCacheSize);
        BlockCacheSize = blockCacheSize;
        InitRootHash();
    }

    public VerkleStateStore(
        IDb leafDb,
        IDb internalDb,
        IDb stateRootToBlocks,
        int blockCacheSize,
        ILogManager? logManager)
    {
        LogManager = logManager!;
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(internalDb, leafDb);
        StateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = new BlockBranchCache(blockCacheSize);
        BlockCacheSize = blockCacheSize;
        InitRootHash();
    }

    public static Span<byte> RootNodeKey => Array.Empty<byte>();
    private Hash256? PersistedStateRoot { get; set; } = Hash256.Zero;

    private long LastPersistedBlockNumber
    {
        get => _latestPersistedBlockNumber;
        set
        {
            if (value != _latestPersistedBlockNumber)
            {
                _latestPersistedBlockNumber = value;
                _lastPersistedReachedReorgBoundary = false;
            }
        }
    }

    private long LatestCommittedBlockNumber { get; set; }


    /// <summary>
    ///     maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int BlockCacheSize { get; }

    /// <summary>
    ///     Cache used to store state changes for each block - used for serving SnapSync and handling reorgs.
    /// </summary>
    private BlockBranchCache BlockCache { get; }

    /// <summary>
    ///     The underlying key value database - to persist the final state
    ///     We try to avoid fetching from this, and we only store at the end of a batch insert
    /// </summary>
    private VerkleKeyValueDb Storage { get; }

    /// <summary>
    ///     Keep track of current state root being used for all the operations
    /// </summary>
    public Hash256 StateRoot { get; private set; } = Hash256.Zero;

    public IReadOnlyVerkleTrieStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(this, keyValueStore);
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        Hash256 stateRootToUse = stateRoot ?? StateRoot;
        var value = BlockCache.GetLeaf(key.ToArray(), stateRootToUse);
        if (value is not null) return value;
        return Storage.GetLeaf(key, out value) ? value : null;
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        Hash256 stateRootToUse = stateRoot ?? StateRoot;
        InternalNode? value = BlockCache.GetInternalNode(key.ToArray(), stateRootToUse);
        if (value is not null) return value;
        return Storage.GetInternalNode(key, out value) ? value : null;
    }

    public bool HasStateForBlock(Hash256 stateRoot)
    {
        Hash256? stateRootToCheck = stateRoot;

        // just a edge case - to account for empty MPT state root
        if (stateRoot.Equals(Keccak.EmptyTreeHash)) stateRootToCheck = Hash256.Zero;
        if (stateRootToCheck == Hash256.Zero) return true;

        // check in cache and then check for the persisted state root
        return BlockCache.GetStateRootNode(stateRootToCheck, out _) || PersistedStateRoot == stateRootToCheck;
    }

    public bool MoveToStateRoot(Hash256 stateRoot)
    {
        if (stateRoot == Hash256.Zero)
        {
            StateRoot = Hash256.Zero;
            return true;
        }

        if (BlockCache.GetStateRootNode(stateRoot, out BlockBranchNode? rootNode))
        {
            StateRoot = rootNode.Data.StateRoot;
            return true;
        }

        if (stateRoot != PersistedStateRoot) return false;

        StateRoot = stateRoot;

        return true;
    }


    /// <summary>
    ///     Events that are used by the VerkleArchiveStore to build the archive index
    /// </summary>
    public event EventHandler<InsertBatchCompletedV1>? InsertBatchCompletedV1;

    public event EventHandler<InsertBatchCompletedV2>? InsertBatchCompletedV2;

    private void InitRootHash()
    {
        if (Storage.GetInternalNode(RootNodeKey, out InternalNode rootNode))
        {
            PersistedStateRoot = StateRoot = new Hash256(rootNode!.InternalCommitment.ToBytes());
            LastPersistedBlockNumber = StateRootToBlocks[StateRoot];
            if (LastPersistedBlockNumber == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
            LatestCommittedBlockNumber = -1;
        }
        else
        {
            Storage.SetInternalNode(RootNodeKey, new InternalNode(VerkleNodeType.BranchNode));
            PersistedStateRoot = StateRoot = Hash256.Zero;
            LastPersistedBlockNumber = LatestCommittedBlockNumber = -1;
        }
    }

    private static Hash256? GetStateRoot(InternalStore db)
    {
        return db.TryGetValue(RootNodeKey, out InternalNode? node) ? node!.Bytes : null;
    }
}
