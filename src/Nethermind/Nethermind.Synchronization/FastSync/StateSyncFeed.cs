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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Logging;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;

namespace Nethermind.Synchronization.FastSync
{
    public partial class StateSyncFeed : SyncFeed<StateSyncBatch?>, IDisposable
    {
        private const StateSyncBatch EmptyBatch = null;

        private readonly Stopwatch _handleWatch = new();
        private readonly ILogger _logger;
        private readonly ISyncModeSelector _syncModeSelector;
        private readonly TreeSync _treeSync;

        public override bool IsMultiFeed => true;

        public override AllocationContexts Contexts => AllocationContexts.State;

        public StateSyncFeed(
            ISyncModeSelector syncModeSelector,
            TreeSync treeSync,
            ILogManager logManager)
        {
            _syncModeSelector = syncModeSelector ?? throw new ArgumentNullException(nameof(syncModeSelector));
            _treeSync = treeSync ?? throw new ArgumentNullException(nameof(treeSync));
            _syncModeSelector.Changed += SyncModeSelectorOnChanged;

            _logger = logManager.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));
        }

        public override async Task<StateSyncBatch?> PrepareRequest(CancellationToken token = default)
        {
            try
            {
                (bool continueProcessing, bool finishSyncRound) = _treeSync.ValidatePrepareRequest(_syncModeSelector.Current);

                if (finishSyncRound)
                {
                    FinishThisSyncRound();
                }

                if (!continueProcessing)
                {
                    return EmptyBatch!;
                }

                return await _treeSync.PrepareRequest(_syncModeSelector.Current);
            }
            catch (Exception e)
            {
                _logger.Error("Error when preparing a batch", e);
                return await Task.FromResult(EmptyBatch);
            }
        }

        ConcurrentDictionary<string, int> clients = new ConcurrentDictionary<string, int>();
        int counter = 0;
        public override SyncResponseHandlingResult HandleResponse(StateSyncBatch? batch, PeerInfo peer = null)
        {
            if(batch != null && peer != null)
            {
                string key = peer.SyncPeer.ClientId;

                if (clients.ContainsKey(key))
                {
                    clients[key] = clients[key] + 1;
                }
                else
                {
                    clients[key] = 1;
                }
            }

            counter++;

            if(counter % 100 == 0)
            {
                PrintStats();
            }
            return _treeSync.HandleResponse(batch);
        }

        public void Dispose()
        {
            _syncModeSelector.Changed -= SyncModeSelectorOnChanged;
        }

        private void SyncModeSelectorOnChanged(object? sender, SyncModeChangedEventArgs e)
        {
            if (CurrentState == SyncFeedState.Dormant)
            {
                if ((e.Current & SyncMode.StateNodes) == SyncMode.StateNodes)
                {
                    _treeSync.ResetStateRootToBestSuggested(CurrentState);
                    Activate();
                }
            }
        }

        private void FinishThisSyncRound()
        {
            PrintStats();

            lock (_handleWatch)
            {
                FallAsleep();
                _treeSync.ResetStateRoot(CurrentState);
            }
        }

        private void PrintStats()
        {
            foreach (var item in clients)
            {
                _logger.Warn($"{item.Key} -> {item.Value}" );
            }
        }
    }
}
