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
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.State.Snap;
using Nethermind.Synchronization.FastSync;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Trie;

namespace Nethermind.Synchronization.SnapSync
{
    public class SnapSyncStateDispatcher : SyncDispatcher<StateSyncBatch>
    {
        public SnapSyncStateDispatcher(ISyncFeed<StateSyncBatch>? syncFeed, ISyncPeerPool? syncPeerPool, IPeerAllocationStrategyFactory<StateSyncBatch>? peerAllocationStrategy, ILogManager? logManager) 
            : base(syncFeed, syncPeerPool, peerAllocationStrategy, logManager)
        {
        }
        private static readonly byte[] _emptyBytes = { 0 };

        protected override async Task Dispatch(PeerInfo peerInfo, StateSyncBatch batch, CancellationToken cancellationToken)
        {
            ISyncPeer peer = peerInfo.SyncPeer;
            if (batch.RequestedNodes is null)
            {
                return;
            }

            if (peer.TryGetSatelliteProtocol<ISnapSyncPeer>("snap", out var handler))
            {
                Dictionary<Keccak, (
                        List<byte[]> Nodes,
                        Dictionary<Keccak, List<byte[]>> Storage, 
                        List<Keccak> Codes)
                    >
                    batches = new();
                
                Dictionary<Keccak, int> batchIndex = new();
                for (int i = 0; i < batch.RequestedNodes.Length; i++)
                {
                    StateSyncItem? rItem = batch.RequestedNodes[i];
                    batchIndex[rItem.Hash] = i;
                    if(!batches.ContainsKey(rItem.RootHash))
                    {
                        batches[rItem.RootHash] =
                            (
                                new List<byte[]>(),
                                new Dictionary<Keccak, List<byte[]>>(),
                                new List<Keccak>()
                            );
                    }
                    switch (rItem.NodeDataType)
                    {
                        case NodeDataType.State:
                            batches[rItem.RootHash].Nodes.Add(
                                rItem.PathNibbles.Select(x => new Nibble(x)).ToArray().ToPackedByteArray());
                            break;
                        case NodeDataType.Storage:
                            var x = rItem.AccountPathNibbles.Select(x => new Nibble(x)).ToArray().ToPackedByteArray();
                            var y = rItem.PathNibbles.Select(x => new Nibble(x)).ToArray().ToPackedByteArray();
                            var nodeHash = new Keccak(x);
                            if(!batches[rItem.RootHash].Storage.ContainsKey(nodeHash))
                            {
                                batches[rItem.RootHash].Storage[nodeHash] = new List<byte[]>();
                            }
                            batches[rItem.RootHash].Storage[nodeHash].Add(y);
                            break;
                        case NodeDataType.Code:
                            batches[rItem.RootHash].Codes.Add(rItem.Hash);
                            break;
                        default:
                            throw new Exception();
                    }
                }

                List<Task> tasks = new();
                batch.Responses = new byte[batch.RequestedNodes.Length][];
                foreach(var root in batches.Keys)
                {
                    var rootBatch = batches[root];
                    var nodePaths =
                        rootBatch.Nodes.Select(x => new PathGroup {Group = new[] {x}})
                            .Concat(rootBatch.Storage.Select(kv =>
                                new PathGroup {Group = new[] {kv.Key.Bytes}.Concat(kv.Value).ToArray()}))
                            .ToArray();
                    Task<byte[][]> nodes = handler.GetTrieNodes(
                        new TrieNodesRequest {RootHash = root, 
                            AccountAndStoragePaths = nodePaths
                        },
                        cancellationToken);

                    tasks.Add(nodes.ContinueWith(
                        (t, state) =>
                        {
                            if (t.IsFaulted)
                            {
                                if (Logger.IsTrace)
                                    Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                            }

                            StateSyncBatch batchLocal = (StateSyncBatch)state!;
                            if (t.IsCompletedSuccessfully)
                            {
                                foreach (var res in t.Result)
                                {
                                    var hash = Keccak.Compute(res);

                                    if (batchIndex.ContainsKey(hash))
                                    {
                                        batchLocal.Responses[batchIndex[hash]] = res;
                                    }
                                    else
                                    {
                                        
                                    }
                                }
                            }
                        }, batch));
                    
                    Task<byte[][]> codes = handler.GetByteCodes(
                    rootBatch.Codes.ToArray()
                    , cancellationToken);

                    tasks.Add(codes.ContinueWith(
                        (t, state) =>
                        {
                            if (t.IsFaulted)
                            {
                                if (Logger.IsTrace)
                                    Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                            }

                            StateSyncBatch batchLocal = (StateSyncBatch)state!;
                            if (t.IsCompletedSuccessfully)
                            {
                                foreach (var res in t.Result)
                                {
                                    var hash = Keccak.Compute(res);

                                    if (batchIndex.ContainsKey(hash))
                                    {
                                        batchLocal.Responses[batchIndex[hash]] = res;
                                    }
                                    else
                                    {
                                        
                                    }
                                }
                            }
                        }, batch));
                }

                await Task.WhenAll(tasks.ToArray()).ContinueWith(x =>
                {
                    Logger.Warn($"STATS: {batch.Responses.Count(xx=>xx is not null)}/{batch.RequestedNodes.Length}");
                    foreach (var root in batches)
                    {
                        Logger.Warn($"STATS: {root.Key}: {root.Value.Nodes.Count}/{root.Value.Storage.Sum(x=>x.Value.Count)}/{root.Value.Codes.Count}");
                    }
                });
            }
        }
    }
}
