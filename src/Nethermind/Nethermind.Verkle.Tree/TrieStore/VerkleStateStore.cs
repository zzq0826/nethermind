using System;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public partial class VerkleStateStore : IVerkleTrieStore, ISyncTrieStore
{
    public static Span<byte> RootNodeKey => Array.Empty<byte>();

    private readonly ILogger _logger;
    public readonly ILogManager LogManager;

    /// <summary>
    ///
    /// </summary>
    private StateInfo? RootNodeData { get; set; }
    public Hash256 StateRoot { get; private set; } = Pedersen.Zero;


    /// <summary>
    ///
    /// </summary>
    private bool _lastPersistedReachedReorgBoundary;
    private long _latestPersistedBlockNumber;

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
    private Hash256? PersistedStateRoot { get; set; } = Pedersen.Zero;

    /// <summary>
    ///
    /// </summary>
    private long LatestCommittedBlockNumber { get; set; }

    /// <summary>
    ///
    /// </summary>
    public readonly StateRootToBlockMap StateRootToBlocks;


    /// <summary>
    ///
    /// </summary>
    public event EventHandler<InsertBatchCompletedV1>? InsertBatchCompletedV1;
    public event EventHandler<InsertBatchCompletedV2>? InsertBatchCompletedV2;


    /// <summary>
    ///  maximum number of blocks that should be stored in cache (not persisted in db)
    /// </summary>
    private int BlockCacheSize { get; }
    private BlockBranchCache BlockCache { get; }

    /// <summary>
    /// The underlying key value database - to persist the final state
    /// We try to avoid fetching from this, and we only store at the end of a batch insert
    /// </summary>
    private VerkleKeyValueDb Storage { get; }


    public VerkleStateStore(IDbProvider dbProvider, int blockCacheSize, ILogManager logManager)
    {
        LogManager = logManager;
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        StateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = new (blockCacheSize);
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
        BlockCache = new (blockCacheSize);
        BlockCacheSize = blockCacheSize;
        InitRootHash();
    }
    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore) => new (this, keyValueStore);

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        InternalNode? rootNode = GetInternalNode(RootNodeKey);
        if (rootNode is not null)
        {
            StateRoot = new Hash256(rootNode.InternalCommitment.ToBytes());
            LastPersistedBlockNumber = StateRootToBlocks[StateRoot];
            if (LastPersistedBlockNumber == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
            LatestCommittedBlockNumber = -1;
        }
        else
        {
            Storage.SetInternalNode(RootNodeKey, new InternalNode(VerkleNodeType.BranchNode));
            StateRoot = Pedersen.Zero;
            LastPersistedBlockNumber = LatestCommittedBlockNumber = -1;
        }
    }

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

    /// <summary>
    ///
    /// </summary>
    /// <param name="stateRoot"></param>
    /// <returns></returns>
    public bool HasStateForBlock(Hash256 stateRoot)
    {
        Hash256? stateRootToCheck = stateRoot;

        // just a edge case - to account for empty MPT state root
        if (stateRoot.Equals(Keccak.EmptyTreeHash)) stateRootToCheck = Pedersen.Zero;

        // check in cache and then check for the persisted state root
        return BlockCache.GetStateRootNode(stateRootToCheck, out _) || PersistedStateRoot == stateRootToCheck;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="stateRoot"></param>
    /// <returns></returns>
    public bool MoveToStateRoot(Hash256 stateRoot)
    {
        if (BlockCache.GetStateRootNode(stateRoot, out BlockBranchNode? rootNode))
        {
            RootNodeData = rootNode.Data;
            StateRoot = rootNode.Data.StateRoot;
            return true;
        }

        // TODO: add here to test if state root is equal to persisted root
        if (StateRootToBlocks[stateRoot] != LastPersistedBlockNumber) return false;

        RootNodeData = null;
        StateRoot = stateRoot;

        return false;
    }

    private static Hash256? GetStateRoot(InternalStore db)
    {
        return db.TryGetValue(RootNodeKey, out InternalNode? node) ? node!.Bytes: null;
    }
}
