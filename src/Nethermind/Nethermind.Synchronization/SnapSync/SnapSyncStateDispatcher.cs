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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
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
            //
            // //TODO: replace with a constant "snap"
            if (peer.TryGetSatelliteProtocol<ISnapSyncPeer>("snap", out var handler))
            {
                foreach(var item in batch.RequestedNodes!.Where(x=>x.NodeDataType == NodeDataType.State))
                {
                    var x = item.PathNibbles.Select(x=> new Nibble(x)).ToArray().ToPackedByteArray();
                    Logger.Warn("ACC REQ:" + item.RootHash + "|" + x.ToHexString());
                    Task<byte[][]> task = handler.GetTrieNodes(new TrieNodesRequest
                    {
                        RootHash = item.RootHash,
                        AccountAndStoragePathes = new []
                        {
                            x
                        }
                    }, cancellationToken);
                    
                    await task.ContinueWith(
                        (t, state) =>
                        {
                            if (t.IsFaulted)
                            {
                                //if (Logger.IsTrace)
                                    Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                            }
                    
                            StateSyncBatch batchLocal = (StateSyncBatch)state!;
                            if (t.IsCompletedSuccessfully)
                            {
                                batchLocal.Responses = t.Result;
                            }
                        }, batch);
                }
                foreach(var item in batch.RequestedNodes!.Where(x=>x.NodeDataType == NodeDataType.Storage))
                {
                    //while (true)
                    //{
                        var x = item.AccountPathNibbles.Select(x => new Nibble(x)).ToArray().ToPackedByteArray();
                        var y = item.PathNibbles.Select(x => new Nibble(x)).ToArray().ToPackedByteArray();
                        Logger.Warn("STR REQ:"+ item.RootHash + "|"  + x.ToHexString() + "," + y.ToHexString());
                        Task<byte[][]> task = handler.GetTrieNodes(
                            new TrieNodesRequest {RootHash = item.RootHash, AccountAndStoragePathes = new[] {x, y}},
                            cancellationToken);
                        await task.ContinueWith(
                            (t, state) =>
                            {
                                if (t.IsFaulted)
                                {
                                    //if (Logger.IsTrace)
                                        Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                                }
                    
                                StateSyncBatch batchLocal = (StateSyncBatch)state!;
                                if (t.IsCompletedSuccessfully)
                                {
                                    batchLocal.Responses = t.Result;
                                }
                            }, batch);
                }
                foreach(var item in batch.RequestedNodes!.Where(x=>x.NodeDataType.HasFlag(NodeDataType.Code)))
                {
                    var x = item.PathNibbles.Select(x=> new Nibble(x)).ToArray().ToPackedByteArray();
                    Logger.Warn("CDE REQ:" + item.RootHash + "|" + item.Hash.ToString());
                    Task<byte[][]> task = handler.GetByteCodes(new []
                    {
                        item.Hash
                    }, cancellationToken);
                    
                    await task.ContinueWith(
                        (t, state) =>
                        {
                            if (t.IsFaulted)
                            {
                                //if (Logger.IsTrace)
                                    Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                            }
                    
                            StateSyncBatch batchLocal = (StateSyncBatch)state!;
                            if (t.IsCompletedSuccessfully)
                            {
                                batchLocal.Responses = t.Result;
                            }
                        }, batch);
                }
                // if (batch.AccountRangeRequest is not null)
                // {
                //     Task<AccountsAndProofs> task = handler.GetAccountRange(batch.AccountRangeRequest, cancellationToken);
                //
                //     await task.ContinueWith(
                //         (t, state) =>
                //         {
                //             if (t.IsFaulted)
                //             {
                //                 if (Logger.IsTrace)
                //                     Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                //             }
                //
                //             SnapSyncBatch batchLocal = (SnapSyncBatch)state!;
                //             if (t.IsCompletedSuccessfully)
                //             {
                //                 batchLocal.AccountRangeResponse = t.Result;
                //             }
                //         }, batch);
                // }
                // else if (batch.StorageRangeRequest is not null)
                // {
                //     Task<SlotsAndProofs> task = handler.GetStorageRange(batch.StorageRangeRequest, cancellationToken);
                //
                //     await task.ContinueWith(
                //         (t, state) =>
                //         {
                //             if (t.IsFaulted)
                //             {
                //                 if (Logger.IsTrace)
                //                     Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                //             }
                //
                //             SnapSyncBatch batchLocal = (SnapSyncBatch)state!;
                //             if (t.IsCompletedSuccessfully)
                //             {
                //                 batchLocal.StorageRangeResponse = t.Result;
                //             }
                //         }, batch);
                // }
                // else if (batch.CodesRequest is not null)
                // {
                //     Task<byte[][]> task = handler.GetByteCodes(batch.CodesRequest, cancellationToken);
                //
                //     await task.ContinueWith(
                //         (t, state) =>
                //         {
                //             if (t.IsFaulted)
                //             {
                //                 if (Logger.IsTrace)
                //                     Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                //             }
                //
                //             SnapSyncBatch batchLocal = (SnapSyncBatch)state!;
                //             if (t.IsCompletedSuccessfully)
                //             {
                //                 batchLocal.CodesResponse = t.Result;
                //             }
                //         }, batch);
                // }
                // else if (batch.AccountsToRefreshRequest is not null)
                // {
                //     Task<byte[][]> task = handler.GetTrieNodes(batch.AccountsToRefreshRequest, cancellationToken);
                //
                //     await task.ContinueWith(
                //         (t, state) =>
                //         {
                //             if (t.IsFaulted)
                //             {
                //                 if (Logger.IsTrace)
                //                     Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", t.Exception);
                //             }
                //
                //             SnapSyncBatch batchLocal = (SnapSyncBatch)state!;
                //             if (t.IsCompletedSuccessfully)
                //             {
                //                 batchLocal.AccountsToRefreshResponse = t.Result;
                //             }
                //         }, batch);
                // }
            }

            await Task.CompletedTask;
        }
    }
}
