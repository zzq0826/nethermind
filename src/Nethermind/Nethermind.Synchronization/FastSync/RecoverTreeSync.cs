using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Synchronization.FastSync
{
    public class RecoverTreeSync: TreeSync
    {
        
        private const StateSyncBatch EmptyBatch = null;

        private static readonly AccountDecoder AccountDecoder = new();

        private readonly DetailedProgress _data;
        private readonly IPendingSyncItems _pendingItems;

        private readonly Keccak _fastSyncProgressKey = Keccak.Zero;

        private DateTime _lastReview = DateTime.UtcNow;
        private DateTime _currentSyncStart;
        private long _currentSyncStartSecondsInSync;

        private readonly object _stateDbLock = new();
        private readonly object _codeDbLock = new();

        private readonly Stopwatch _networkWatch = new();
        private readonly Stopwatch _handleWatch = new();

        private Keccak _rootNode { get; set; } = Keccak.EmptyTreeHash;
        private int _rootSaved;


        private readonly IBlockTree _blockTree;

        private readonly ConcurrentDictionary<StateSyncBatch, object?> _pendingRequests = new();
        private Dictionary<Keccak, HashSet<DependentItem>> _dependencies = new();
        private LruKeyCache<Keccak> _alreadySaved = new(AlreadySavedCapacity, "saved nodes");
        private readonly HashSet<Keccak> _codesSameAsNodes = new();

        private BranchProgress _branchProgress;
        private int _hintsToResetRoot;
        private long _blockNumber;
        private Keccak accountHash;
        private Keccak? storageHash;

        public RecoverTreeSync(SyncMode syncMode, IDb codeDb, IDb stateDb, IBlockTree blockTree, ILogManager logManager) : base(syncMode, codeDb, stateDb, blockTree, logManager)
        {
        }

        public SyncResponseHandlingResult HandleResponse(StateSyncBatch? batch)
        {
            if (batch == EmptyBatch)
            {
                if (_logger.IsError) _logger.Error("Received empty batch as a response");
                return SyncResponseHandlingResult.InternalError;
            }

            if (_logger.IsTrace) _logger.Trace($"Removing pending request {batch}");
            if (!_pendingRequests.TryRemove(batch, out _))
            {
                if (_logger.IsDebug) _logger.Debug($"Cannot remove pending request {batch}");
                return SyncResponseHandlingResult.OK;
            }

            int requestLength = batch.RequestedNodes?.Length ?? 0;
            int responseLength = batch.Responses?.Length ?? 0;

            void AddAgainAllItems()
            {
                for (int i = 0; i < requestLength; i++)
                {
                    AddNodeToPending(batch.RequestedNodes![i], null, "missing", true);
                }
            }

            try
            {
                lock (_handleWatch)
                {
                    if (DateTime.UtcNow - _lastReview > TimeSpan.FromSeconds(60))
                    {
                        _lastReview = DateTime.UtcNow;
                        string reviewMessage = _pendingItems.RecalculatePriorities();
                        if (_logger.IsDebug) _logger.Debug(reviewMessage);
                    }

                    _handleWatch.Restart();

                    bool isMissingRequestData = batch.RequestedNodes == null;
                    if (isMissingRequestData)
                    {
                        _hintsToResetRoot++;

                        AddAgainAllItems();
                        if (_logger.IsWarn) _logger.Warn("Batch response had invalid format");
                        Interlocked.Increment(ref _data.InvalidFormatCount);
                        return isMissingRequestData ? SyncResponseHandlingResult.InternalError : SyncResponseHandlingResult.NotAssigned;
                    }

                    if (batch.Responses == null)
                    {
                        AddAgainAllItems();
                        if (_logger.IsTrace) _logger.Trace("Batch was not assigned to any peer.");
                        Interlocked.Increment(ref _data.NotAssignedCount);
                        return SyncResponseHandlingResult.NotAssigned;
                    }

                    if (_logger.IsTrace) _logger.Trace($"Received node data - {responseLength} items in response to {requestLength}");
                    int nonEmptyResponses = 0;
                    int invalidNodes = 0;
                    for (int i = 0; i < batch.RequestedNodes!.Length; i++)
                    {
                        StateSyncItem currentStateSyncItem = batch.RequestedNodes[i];

                        /* if the peer has limit on number of requests in a batch then the response will possibly be
                           shorter than the request */
                        if (batch.Responses.Length < i + 1)
                        {
                            AddNodeToPending(currentStateSyncItem, null, "missing", true);
                            continue;
                        }

                        /* if the peer does not have details of this particular node */
                        byte[] currentResponseItem = batch.Responses[i];
                        if (currentResponseItem == null)
                        {
                            AddNodeToPending(batch.RequestedNodes[i], null, "missing", true);
                            continue;
                        }

                        /* node sent data that is not consistent with its hash - it happens surprisingly often */
                        if (!ValueKeccak.Compute(currentResponseItem).BytesAsSpan.SequenceEqual(currentStateSyncItem.Hash.Bytes))
                        {
                            AddNodeToPending(currentStateSyncItem, null, "missing", true);
                            if (_logger.IsWarn) _logger.Warn($"Peer sent invalid data (batch {requestLength}->{responseLength}) of length {batch.Responses[i]?.Length} of type {batch.RequestedNodes[i].NodeDataType} at level {batch.RequestedNodes[i].Level} of type {batch.RequestedNodes[i].NodeDataType} Keccak({batch.Responses[i].ToHexString()}) != {batch.RequestedNodes[i].Hash}");
                            invalidNodes++;
                            continue;
                        }

                        nonEmptyResponses++;
                        NodeDataType nodeDataType = currentStateSyncItem.NodeDataType;
                        if (nodeDataType == NodeDataType.Code)
                        {
                            SaveNode(currentStateSyncItem, currentResponseItem);
                            continue;
                        }

                        HandleTrieNode(currentStateSyncItem, currentResponseItem, ref invalidNodes);
                    }

                    Interlocked.Add(ref _data.ConsumedNodesCount, nonEmptyResponses);
                    StoreProgressInDb();

                    if (_logger.IsTrace) _logger.Trace($"After handling response (non-empty responses {nonEmptyResponses}) of {batch.RequestedNodes.Length} from ({_pendingItems.Description}) nodes");

                    /* magic formula is ratio of our desired batch size - 1024 to Geth max batch size 384 times some missing nodes ratio */
                    bool isEmptish = (decimal)nonEmptyResponses / Math.Max(requestLength, 1) < 384m / 1024m * 0.75m;
                    if (isEmptish)
                    {
                        Interlocked.Increment(ref _hintsToResetRoot);
                        Interlocked.Increment(ref _data.EmptishCount);
                    }
                    else
                    {
                        Interlocked.Exchange(ref _hintsToResetRoot, 0);
                    }

                    /* here we are very forgiving for Geth nodes that send bad data fast */
                    bool isBadQuality = nonEmptyResponses > 64 && (decimal)invalidNodes / Math.Max(requestLength, 1) > 0.50m;
                    if (isBadQuality) Interlocked.Increment(ref _data.BadQualityCount);

                    bool isEmpty = nonEmptyResponses == 0 && !isBadQuality;
                    if (isEmpty)
                    {
                        if (_logger.IsDebug) _logger.Debug($"Peer sent no data in response to a request of length {batch.RequestedNodes.Length}");
                        return SyncResponseHandlingResult.NoProgress;
                    }

                    if (!isEmptish && !isBadQuality)
                    {
                        Interlocked.Increment(ref _data.OkCount);
                    }

                    SyncResponseHandlingResult result = isEmptish
                        ? SyncResponseHandlingResult.Emptish
                        : isBadQuality
                            ? SyncResponseHandlingResult.LesserQuality
                            : SyncResponseHandlingResult.OK;

                    _data.DisplayProgressReport(_pendingRequests.Count, _branchProgress, _logger);

                    long total = _handleWatch.ElapsedMilliseconds + _networkWatch.ElapsedMilliseconds;
                    if (total != 0)
                    {
                        // calculate averages
                        if (_logger.IsTrace) _logger.Trace($"Prepare batch {_networkWatch.ElapsedMilliseconds}ms ({(decimal)_networkWatch.ElapsedMilliseconds / total:P0}) - Handle {_handleWatch.ElapsedMilliseconds}ms ({(decimal)_handleWatch.ElapsedMilliseconds / total:P0})");
                    }

                    if (_handleWatch.ElapsedMilliseconds > 250)
                    {
                        if (_logger.IsDebug) _logger.Debug($"Handle watch {_handleWatch.ElapsedMilliseconds}, DB reads {_data.DbChecks - _data.LastDbReads}, ratio {(decimal)_handleWatch.ElapsedMilliseconds / Math.Max(1, _data.DbChecks - _data.LastDbReads)}");
                    }

                    _data.LastDbReads = _data.DbChecks;
                    _data.AverageTimeInHandler = (_data.AverageTimeInHandler * (_data.ProcessedRequestsCount - 1) + _handleWatch.ElapsedMilliseconds) / _data.ProcessedRequestsCount;
                    Interlocked.Add(ref _data.HandledNodesCount, nonEmptyResponses);
                    return result;
                }
            }
            catch (Exception e)
            {
                _logger.Error("Error when handling state sync response", e);
                return SyncResponseHandlingResult.InternalError;
            }
            finally
            {
                _handleWatch.Stop();
            }
        }

        internal void Recover(long number, Keccak state, SyncFeedState currentState, Keccak accountHash, Keccak? storageHash)
        {
            this.accountHash = accountHash;
            this.storageHash = storageHash;
            ResetStateRoot(number, state, currentState);
        }

        public (bool continueProcessing, bool finishSyncRound) ValidatePrepareRequest(SyncMode currentSyncMode)
        {
            if (_rootSaved == 1)    
            {
                VerifyPostSyncCleanUp();
                return (false, true);
            }

            if ((currentSyncMode & _syncMode) != _syncMode)
            {
                return (false, false);
            }

            if (_rootNode == Keccak.EmptyTreeHash)
            {
                if (_logger.IsDebug) _logger.Debug("Falling asleep - root is empty tree");
                return (false, true);
            }

            if (_hintsToResetRoot >= 32)
            {
                if (_logger.IsDebug) _logger.Debug("Falling asleep - many missing responses");
                return (false, true);
            }

            bool rootNodeKeyExists;
            lock (_stateDbLock)
            {
                try
                {
                    // it finished downloading
                    rootNodeKeyExists = _stateDb.KeyExists(_rootNode);
                }
                catch (ObjectDisposedException)
                {
                    return (false, false);
                }
            }

            if (rootNodeKeyExists)
            {
                try
                {
                    _logger.Info($"STATE SYNC FINISHED:{Metrics.StateSyncRequests}, {Metrics.SyncedStateTrieNodes}");
                    RecoverySaga.Instance.Finish();
                    VerifyPostSyncCleanUp();
                    return (false, true);
                }
                catch (ObjectDisposedException)
                {
                    return (false, false);
                }
            }

            return (true, false);
        }

    }
}
