using System;
using System.Collections.Generic;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Synchronization.FastSync
{
    public class RecoverTreeSync : TreeSync
    {
        private Keccak accountHash;
        private Keccak? storageHash;

        public RecoverTreeSync(SyncMode syncMode, IDb codeDb, IDb stateDb, IBlockTree blockTree, ILogManager logManager) : base(syncMode, codeDb, stateDb, blockTree, logManager)
        {
        }

        internal void Recover(long number, Keccak state, SyncFeedState currentState, Keccak accountHash, Keccak? storageHash)
        {
            this.accountHash = accountHash;
            this.storageHash = storageHash;
            ResetStateRoot(number, state, currentState);
        }

        public override (bool continueProcessing, bool finishSyncRound) ValidatePrepareRequest(SyncMode currentSyncMode)
        {
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

            bool requestedNodeExists;
            lock (_stateDbLock)
            {
                try
                {
                    // it finished downloading
                    requestedNodeExists = storageHash is not null ? _stateDb.KeyExists(storageHash) : _stateDb.KeyExists(accountHash);
                }
                catch (ObjectDisposedException)
                {
                    return (false, false);
                }
            }

            if (requestedNodeExists && _pendingItems.Count == 0)
            {
                try
                {
                    _logger.Info($"STATE RECOVERY FINISHED:{Metrics.StateSyncRequests}, {Metrics.SyncedStateTrieNodes}");
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

        protected override AddNodeResult AddNodeToPending(StateSyncItem syncItem, DependentItem? dependentItem, string reason, bool missing = false)
        {
            bool MuhBranch(byte[] sourcePath, Span<byte> myPath)
            {
                for (var i = 0; i < myPath.Length; i++)
                    if (myPath[i] != sourcePath[i])
                        return false;
                return true;
            }

            var currentRes = base.AddNodeToPending(syncItem, dependentItem, reason, missing);
            if (currentRes == AddNodeResult.AlreadySaved)
            {

                Span<byte> branchChildPath = stackalloc byte[syncItem.PathNibbles.Length + 1];
                syncItem.PathNibbles.CopyTo(branchChildPath.Slice(0, syncItem.PathNibbles.Length));
                var src = Nibbles.NibbleBytesFromBytes(accountHash.Bytes);
                if(!MuhBranch(src, syncItem.PathNibbles))
                {
                    return currentRes;
                }

                object lockToTake = syncItem.NodeDataType == NodeDataType.Code ? _codeDbLock : _stateDbLock;
                lock (lockToTake)
                {
                    IDb dbToCheck = syncItem.NodeDataType == NodeDataType.Code ? _codeDb : _stateDb;
                    Interlocked.Increment(ref _data.DbChecks);
                    TrieNode trieNode = new(NodeType.Unknown, syncItem.Hash, dbToCheck.Get(syncItem.Hash));
                    trieNode.ResolveNode(NullTrieNodeResolver.Instance);

                    switch (trieNode.NodeType)
                    {
                        case NodeType.Branch:
                            HashSet<Keccak?> alreadyProcessedChildHashes = new();

                            for (int childIndex = 15; childIndex >= 0; childIndex--)
                            {
                                Keccak? childHash = trieNode.GetChildHash(childIndex);
                                if (childHash != null &&
                                    alreadyProcessedChildHashes.Contains(childHash))
                                {
                                    continue;
                                }

                                alreadyProcessedChildHashes.Add(childHash);

                                if (childHash != null)
                                {
                                    branchChildPath[syncItem.PathNibbles.Length] = (byte)childIndex;

                                    AddNodeResult addChildResult = AddNodeToPending(
                                        new StateSyncItem(childHash, syncItem.AccountPathNibbles, branchChildPath.ToArray(), syncItem.NodeDataType, syncItem.Level + 1, CalculateRightness(trieNode.NodeType, syncItem, childIndex))
                                        {
                                            BranchChildIndex = (short)childIndex,
                                            ParentBranchChildIndex = syncItem.BranchChildIndex
                                        },
                                        null,
                                        "branch child");
                                    if(addChildResult != AddNodeResult.AlreadySaved)
                                    {
                                        currentRes = addChildResult;
                                    }
                                }
                            }
                            break;

                        case NodeType.Extension:
                            Keccak? next = trieNode.GetChild(NullTrieNodeResolver.Instance, 0)?.Keccak;
                            if (next != null)
                            {

                                Span<byte> childPath = stackalloc byte[syncItem.PathNibbles.Length + trieNode.Path!.Length];
                                syncItem.PathNibbles.CopyTo(childPath.Slice(0, syncItem.PathNibbles.Length));
                                trieNode.Path!.CopyTo(childPath.Slice(syncItem.PathNibbles.Length));

                                currentRes = AddNodeToPending(
                                    new StateSyncItem(
                                        next,
                                        syncItem.AccountPathNibbles,
                                        childPath.ToArray(),
                                        syncItem.NodeDataType,
                                        syncItem.Level + trieNode.Path!.Length,
                                        CalculateRightness(trieNode.NodeType, syncItem, 0))
                                    { ParentBranchChildIndex = syncItem.BranchChildIndex },
                                    null,
                                    "extension child");
                            }
                            break;
                    }
                }
            }
            return currentRes;
        }

        protected override void SaveNode(StateSyncItem syncItem, byte[] data)
        {
            _logger.Warn($"~~~~~~ RECOVER {syncItem.Hash}");
            base.SaveNode(syncItem, data);
        }
    }
}
