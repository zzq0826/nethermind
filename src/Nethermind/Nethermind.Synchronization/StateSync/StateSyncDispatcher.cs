//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.State.Snap;
using Nethermind.Synchronization.FastSync;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Trie;

namespace Nethermind.Synchronization.StateSync
{
    public class StateSyncDispatcher : SyncDispatcher<StateSyncBatch>
    {
        //private string _trace;

        private readonly bool _snapSyncEnabled;

        public StateSyncDispatcher(ISyncFeed<StateSyncBatch> syncFeed, ISyncPeerPool syncPeerPool, IPeerAllocationStrategyFactory<StateSyncBatch> peerAllocationStrategy, ISyncConfig syncConfig, ILogManager logManager)
            : base(syncFeed, syncPeerPool, peerAllocationStrategy, logManager)
        {
            _snapSyncEnabled = syncConfig.SnapSync;
        }



        ConcurrentDictionary<string, int> clients = new ConcurrentDictionary<string, int>();
        private void InsertStats(ISyncPeer peer, string method)
        {
            string key = method + ":" + peer.ClientId;

            if (clients.ContainsKey(key))
            {
                clients[key] = clients[key] + 1;
            }
            else
            {
                clients[key] = 1;
            }
        }

        protected override void SyncFeedOnStateChanged(object? sender, SyncFeedStateEventArgs e)
        {
            base.SyncFeedOnStateChanged(sender, e);
            Logger.Warn($"StateNode STATE CHANGED to {e.NewState}");
            PrintStats();
        }

        private void PrintStats()
        {
            foreach (var item in clients)
            {
                Logger.Warn($"{item.Key} -> {item.Value}");
            }
        }

        int counter = 0;
        protected override async Task Dispatch(PeerInfo peerInfo, StateSyncBatch batch, CancellationToken cancellationToken)
        {
            if (batch == null || batch.RequestedNodes == null || batch.RequestedNodes.Length == 0)
            {
                return;
            }

            counter++;

            //string printByteArray(byte[] bytes)
            //{
            //    return string.Join(null, bytes.Select(b => b.ToString("X")));
            //}
            //_trace += String.Join(Environment.NewLine, batch.RequestedNodes.Select(n => n.Level + "|" + n.NodeDataType + "|" + printByteArray(n.AccountPathNibbles) + "|" + printByteArray(n.PathNibbles)))
            //    + Environment.NewLine;

            ISyncPeer peer = peerInfo.SyncPeer;

            Task<byte[][]> task = null;

            //if (_snapSyncEnabled)
            //{
            if (peer.TryGetSatelliteProtocol<ISnapSyncPeer>("snap", out var handler))
            {
                if (batch.NodeDataType == NodeDataType.Code)
                {
                    var a = batch.RequestedNodes.Select(n => n.Hash).ToArray();
                    //Logger.Trace($"GETBYTECODES count:{a.Length}");
                    task = handler.GetByteCodes(a, cancellationToken);

                    InsertStats(peer, "GETBYTECODES");
                }
                else
                {
                    GetTrieNodesRequest request = GetRequest(batch);

                    //Logger.Trace($"GETTRIENODES count:{request.AccountAndStoragePaths.Length}");

                    task = handler.GetTrieNodes(request, cancellationToken);

                    InsertStats(peer, "GETTRIENODES");
                }


            }
            //}

            //if (task == null)
            //{
            //    return;
            //}

            if (task is null)
            {
                var a = batch.RequestedNodes.Select(n => n.Hash).ToArray();
                //Logger.Warn($"GETNODEDATA count:{a.Length}");

                task = peer.GetNodeData(a, cancellationToken);

                InsertStats(peer, "GETNODEDATA");
            }

            if (counter % 10000 == 0)
            {
                Logger.Warn("1000 requests");
                PrintStats();
            }

            await task.ContinueWith(
                (t, state) =>
                {
                    if (t.IsFaulted)
                    {
                        if (Logger.IsTrace) Logger.Error("DEBUG/ERROR Error after dispatching the state sync request", t.Exception);
                    }

                    StateSyncBatch batchLocal = (StateSyncBatch)state!;
                    if (t.IsCompletedSuccessfully)
                    {
                        batchLocal.Responses = t.Result;
                    }
                }, batch);
        }

        private GetTrieNodesRequest GetRequest(StateSyncBatch batch)
        {
            GetTrieNodesRequest request = new();
            request.RootHash = batch.StateRoot;

            Dictionary<byte[], List<(byte[] path, StateSyncItem syncItem)>> dict = new(Bytes.EqualityComparer);
            List<(byte[] path, StateSyncItem syncItem)> accountTreePaths = new();

            foreach (var item in batch.RequestedNodes)
            {
                if (item.AccountPathNibbles == null || item.AccountPathNibbles.Length == 0)
                {
                    accountTreePaths.Add((item.PathNibbles, item));
                }
                else
                {
                    if (!dict.TryGetValue(item.AccountPathNibbles, out var storagePaths))
                    {
                        storagePaths = new List<(byte[], StateSyncItem)>();
                        dict[item.AccountPathNibbles] = storagePaths;
                    }

                    storagePaths.Add((item.PathNibbles, item));
                }
            }

            request.AccountAndStoragePaths = new PathGroup[accountTreePaths.Count + dict.Count];

            int requestedNodeIndex = 0;
            int accountPathIndex = 0;
            for (; accountPathIndex < accountTreePaths.Count; accountPathIndex++)
            {
                (byte[] path, StateSyncItem syncItem) accountPath = accountTreePaths[accountPathIndex];
                request.AccountAndStoragePaths[accountPathIndex] = new PathGroup() { Group = new[] { EncodePath(accountPath.path) } };

                batch.RequestedNodes[requestedNodeIndex] = accountPath.syncItem;

                requestedNodeIndex++;
            }

            foreach (var kvp in dict)
            {
                byte[][] group = new byte[kvp.Value.Count + 1][];
                group[0] = EncodePath(kvp.Key);

                for (int groupIndex = 1; groupIndex < group.Length; groupIndex++)
                {
                    (byte[] path, StateSyncItem syncItem) storagePath = kvp.Value[groupIndex - 1];
                    group[groupIndex] = EncodePath(storagePath.path);
                    batch.RequestedNodes[requestedNodeIndex] = storagePath.syncItem;

                    requestedNodeIndex++;
                }

                request.AccountAndStoragePaths[accountPathIndex] = new PathGroup() { Group = group };

                accountPathIndex++;
            }

            if (batch.RequestedNodes.Length != requestedNodeIndex)
                Logger.Warn($"INCORRECT number of paths RequestedNodes.Length:{batch.RequestedNodes.Length} <> requestedNodeIndex:{requestedNodeIndex}");

            return request;
        }

        private byte[] EncodePath(byte[] input) => input.Length == 64 ? Nibbles.ToBytes(input) : Nibbles.ToCompactHexEncoding(input);
    }
}
