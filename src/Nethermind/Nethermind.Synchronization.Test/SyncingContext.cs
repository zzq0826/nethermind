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
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Consensus;
using Nethermind.Consensus.Validators;
using Nethermind.Core.Timers;
using Nethermind.Db;
using Nethermind.Db.Blooms;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using Nethermind.State.Repositories;
using Nethermind.State.Witnesses;
using Nethermind.Stats;
using Nethermind.Synchronization.Blocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Trie.Pruning;
using NSubstitute;

namespace Nethermind.Synchronization.Test;

public class SyncingContext : SyncingContextBase
{
    protected sealed override BlockTree BlockTree { get; }
    protected override ISyncServer SyncServer { get; }
    protected sealed override ISynchronizer Synchronizer { get; }
    protected sealed override ISyncPeerPool SyncPeerPool { get; }
    public SyncingContext(SynchronizerType synchronizerType)
    {
        ISyncConfig GetSyncConfig() =>
            synchronizerType switch
            {
                SynchronizerType.Fast => SyncConfig.WithFastSync,
                SynchronizerType.Eth2Merge => SyncConfig.WithEth2Merge,
                SynchronizerType.Full => SyncConfig.WithFullSyncOnly,
                _ => throw new ArgumentOutOfRangeException(nameof(synchronizerType), synchronizerType, null)
            };

        _logger = _logManager.GetClassLogger();
        ISyncConfig syncConfig = GetSyncConfig();
        IDbProvider dbProvider = TestMemDbProvider.Init();
        IDb stateDb = new MemDb();
        IDb codeDb = dbProvider.CodeDb;
        MemDb blockInfoDb = new();
        BlockTree = new BlockTree(new MemDb(), new MemDb(), blockInfoDb,
            new ChainLevelInfoRepository(blockInfoDb),
            new SingleReleaseSpecProvider(Constantinople.Instance, 1), NullBloomStorage.Instance, _logManager);
        ITimerFactory timerFactory = Substitute.For<ITimerFactory>();
        NodeStatsManager stats = new(timerFactory, _logManager);
        SyncProgressResolver syncProgressResolver = new(
            BlockTree,
            NullReceiptStorage.Instance,
            stateDb,
            new TrieStore(stateDb, LimboLogs.Instance),
            syncConfig,
            _logManager);
        SyncPeerPool = new SyncPeerPool(BlockTree, stats,
            new TotalDifficultyBasedBetterPeerStrategy(syncProgressResolver, LimboLogs.Instance), 25, _logManager);

        TotalDifficultyBasedBetterPeerStrategy bestPeerStrategy = new(syncProgressResolver, LimboLogs.Instance);
        MultiSyncModeSelector syncModeSelector = new(syncProgressResolver, SyncPeerPool,
            syncConfig, No.BeaconSync, bestPeerStrategy, _logManager);
        Pivot pivot = new(syncConfig);
        BlockDownloaderFactory blockDownloaderFactory = new(MainnetSpecProvider.Instance,
            BlockTree,
            NullReceiptStorage.Instance,
            Always.Valid,
            Always.Valid,
            SyncPeerPool,
            stats,
            syncModeSelector,
            syncConfig,
            pivot,
            new TotalDifficultyBasedBetterPeerStrategy(syncProgressResolver, _logManager),
            _logManager);
        Synchronizer = new Synchronizer(
            dbProvider,
            MainnetSpecProvider.Instance,
            BlockTree,
            NullReceiptStorage.Instance,
            SyncPeerPool,
            stats,
            syncModeSelector,
            syncConfig,
            blockDownloaderFactory,
            pivot,
            _logManager);

        SyncServer = new SyncServer(
            stateDb,
            codeDb,
            BlockTree,
            NullReceiptStorage.Instance,
            Always.Valid,
            Always.Valid,
            SyncPeerPool,
            syncModeSelector,
            syncConfig,
            new WitnessCollector(new MemDb(), LimboLogs.Instance),
            Policy.FullGossip,
            _logManager);

        SyncPeerPool.Start();

        Synchronizer.Start();
        Synchronizer.SyncEvent += SynchronizerOnSyncEvent;

        _allInstances.Enqueue(this);
    }
}
