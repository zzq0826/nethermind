using System.Diagnostics;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public partial class VerkleStateStore : IVerkleTrieStore, ISyncTrieStore
{
    private static Span<byte> RootNodeKey => Array.Empty<byte>();
    private InternalNode? RootNode { get; set; }
    public VerkleCommitment StateRoot { get; private set; } = VerkleCommitment.Zero;

    private InternalNode? GetRootNode() => GetInternalNode(RootNodeKey);

    private readonly ILogger _logger;

    public VerkleCommitment GetStateRoot()
    {
        InternalNode rootNode = GetRootNode() ?? throw new InvalidOperationException("Root node should always be present");
        byte[] stateRoot = rootNode.Bytes;
        return new VerkleCommitment(stateRoot);
    }

    private static VerkleCommitment? GetStateRoot(IVerkleDb db)
    {
        return db.GetInternalNode(RootNodeKey, out InternalNode? node) ? new VerkleCommitment(node!.Bytes) : null;
    }

    private static VerkleCommitment? GetStateRoot(InternalStore db)
    {
        return db.TryGetValue(RootNodeKey, out InternalNode? node) ? new VerkleCommitment(node!.Bytes) : null;
    }

    // The underlying key value database
    // We try to avoid fetching from this, and we only store at the end of a batch insert
    private VerkleKeyValueDb Storage { get; }

    public VerkleStateStore(IDbProvider dbProvider, ILogManager logManager, int maxNumberOfBlocksInCache = 128)
    {
        _logger = logManager?.GetClassLogger<VerkleStateStore>() ?? throw new ArgumentNullException(nameof(logManager));
        Storage = new VerkleKeyValueDb(dbProvider);
        History = new VerkleHistoryStore(dbProvider, logManager);
        StateRootToBlocks = new StateRootToBlockMap(dbProvider.StateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new (maxNumberOfBlocksInCache);
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
        History = new VerkleHistoryStore(forwardDiff, reverseDiff, logManager);
        StateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new (maxNumberOfBlocksInCache);
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
        StateRootToBlocks = new StateRootToBlockMap(stateRootToBlocks);
        BlockCache = maxNumberOfBlocksInCache == 0
            ? null
            : new (maxNumberOfBlocksInCache);
        MaxNumberOfBlocksInCache = maxNumberOfBlocksInCache;
        InitRootHash();
    }
    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore) => new (this, keyValueStore);

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;

    private void InitRootHash()
    {
        InternalNode? rootNode = GetRootNode();
        if (rootNode is not null)
        {
            StateRoot = new VerkleCommitment(rootNode.InternalCommitment.ToBytes());
            LastPersistedBlockNumber = StateRootToBlocks[StateRoot];
            if (LastPersistedBlockNumber == -2) throw new Exception("StateRoot To BlockNumber Cache Corrupted");
            LatestCommittedBlockNumber = -1;
        }
        else
        {
            Storage.SetInternalNode(RootNodeKey, new InternalNode(VerkleNodeType.BranchNode));
            StateRoot = VerkleCommitment.Zero;
            LastPersistedBlockNumber = LatestCommittedBlockNumber = -1;
        }
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key)
    {
        byte[]? value = BlockCache?.GetLeaf(key.ToArray());
        if (value is not null) return value;
        return Storage.GetLeaf(key, out value) ? value : null;
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key)
    {
        InternalNode? value = BlockCache?.GetInternalNode(key.ToArray());
        if (value is not null) return value;
        return Storage.GetInternalNode(key, out value) ? value : null;
    }

    public bool MoveToStateRoot(VerkleCommitment stateRoot)
    {
        if (StateRoot == stateRoot) return true;

        if (_logger.IsDebug) _logger.Debug($"Trying to move state root from:{StateRoot} to:{stateRoot}");


        // this is to handle a edge case and should be removed eventually - this can be potential issue here
        {
            // TODO: this is actually not possible - not sure if return true is correct here
            if (stateRoot.Equals(new VerkleCommitment(Keccak.EmptyTreeHash.Bytes.ToArray())))
            {
                if (StateRoot.Equals(VerkleCommitment.Zero)) return true;
                return false;
            }
        }

        // resolve block numbers
        long fromBlock = StateRootToBlocks[StateRoot];
        if (fromBlock == -1)
        {
            if (_logger.IsDebug) _logger.Debug($"Cannot get the block number for currentRoot:{StateRoot}");
            return false;
        }
        long toBlock = StateRootToBlocks[stateRoot];
        if (toBlock == -1)
        {
            if (_logger.IsDebug) _logger.Debug($"Cannot get the block number for wantedStateRoot:{stateRoot}");
            return false;
        }

        if (_logger.IsDebug)
            _logger.Debug($"Block numbers resolved. Trying to move state from:{fromBlock} to:{toBlock}");

        // does this ever happen? because we already check for same state root and it is not possible for different
        // stateRoots to have same blockNumbers.
        Debug.Assert(fromBlock != toBlock);

        if (fromBlock > toBlock)
        {
            long noOfBlockToMove = fromBlock - toBlock;
            if (BlockCache is not null && noOfBlockToMove > BlockCache.Count)
            {
                if (_logger.IsDebug)
                    _logger.Debug(
                        $"Number of blocks to move:{noOfBlockToMove}. Removing all the diffs from BlockCache ({noOfBlockToMove} > {BlockCache.Count})");

                if (History is null)
                {
                    if (_logger.IsDebug) _logger.Debug($"History is null and in this case - state cannot be reverted to wanted state root");
                    return false;
                }
                BlockCache.Clear();
                fromBlock -= BlockCache.Count;

                if (_logger.IsDebug)
                    _logger.Debug($"now using fromBlock:{fromBlock} toBlock:{toBlock}");
                BatchChangeSet batchDiff = History.GetBatchDiff(fromBlock, toBlock);
                ApplyDiffLayer(batchDiff);
            }
            else if (BlockCache is not null)
            {
                BlockCache.RemoveDiffs(noOfBlockToMove);
            }
            else
            {
                if (_logger.IsDebug)
                    _logger.Debug(
                        $"BlockCache is null and in this case - state cannot be reverted to wanted state root");
                return false;
            }
        }
        else
        {
            if (_logger.IsDebug)
                _logger.Debug($"Trying to move forward in state - this is not implemented and supported yet");
            return false;
        }

        Debug.Assert(GetStateRoot().Equals(stateRoot));
        LatestCommittedBlockNumber = toBlock;
        return true;
    }

    private void UpdateStateRoot()
    {
        RootNode = GetRootNode();
        StateRoot = RootNode == null
            ? VerkleCommitment.Zero
            : new VerkleCommitment(RootNode!.InternalCommitment.ToBytes());
    }
}
