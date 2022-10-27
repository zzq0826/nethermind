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
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Logging;
using Nethermind.State.Snap;
using Nethermind.Synchronization.FastSync;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;

namespace Nethermind.Synchronization.SnapSync
{
    public class SnapSyncDispatcher : SyncDispatcher<SnapSyncBatch>
    {
        public SnapSyncDispatcher(ISyncFeed<SnapSyncBatch>? syncFeed, ISyncPeerPool? syncPeerPool, IPeerAllocationStrategyFactory<SnapSyncBatch>? peerAllocationStrategy, ILogManager? logManager)
            : base(syncFeed, syncPeerPool, peerAllocationStrategy, logManager)
        {
        }

        protected override async Task Dispatch(PeerInfo peerInfo, SnapSyncBatch batch, CancellationToken cancellationToken)
        {
            ISyncPeer peer = peerInfo.SyncPeer;

            //TODO: replace with a constant "snap"
            if (peer.TryGetSatelliteProtocol<ISnapSyncPeer>("snap", out var handler))
            {
                try
                {
                    if (batch.AccountRangeRequest is not null)
                    {
                        batch.AccountRangeResponse =
                            await handler.GetAccountRange(batch.AccountRangeRequest, cancellationToken);
                    }
                    else if (batch.StorageRangeRequest is not null)
                    {
                        batch.StorageRangeResponse =
                            await handler.GetStorageRange(batch.StorageRangeRequest, cancellationToken);
                    }
                    else if (batch.CodesRequest is not null)
                    {
                        batch.CodesResponse = await handler.GetByteCodes(batch.CodesRequest, cancellationToken);
                    }
                    else if (batch.AccountsToRefreshRequest is not null)
                    {
                        batch.AccountsToRefreshResponse = await handler.GetTrieNodes(batch.AccountsToRefreshRequest, cancellationToken);
                    }
                }
                catch (Exception exception)
                {
                    if (Logger.IsTrace)
                        Logger.Error("DEBUG/ERROR Error after dispatching the snap sync request", exception);
                    throw;
                }
            }

            await Task.CompletedTask;
        }
    }
}
