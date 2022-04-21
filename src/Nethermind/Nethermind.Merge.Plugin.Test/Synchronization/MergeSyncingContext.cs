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
using Nethermind.Merge.Plugin.Synchronization;
using Nethermind.Specs;
using Nethermind.Specs.Forks;
using Nethermind.State.Repositories;
using Nethermind.State.Witnesses;
using Nethermind.Stats;
using Nethermind.Synchronization;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.Test;
using Nethermind.Trie.Pruning;
using NSubstitute;
using No = Nethermind.Synchronization.No;

namespace Nethermind.Merge.Plugin.Test.Synchronization;

public class MergeSyncingContext : SyncingContextBase
{
    protected sealed override BlockTree BlockTree { get; }
    protected override ISyncServer SyncServer { get; }
    protected sealed override ISynchronizer Synchronizer { get; }
    protected sealed override ISyncPeerPool SyncPeerPool { get; }
    
    public MergeSyncingContext(SynchronizerType synchronizerType)
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
        SingleReleaseSpecProvider specProvider = new(Constantinople.Instance, 1);
        BlockTree = new BlockTree(new MemDb(), new MemDb(), blockInfoDb,
            new ChainLevelInfoRepository(blockInfoDb),
            specProvider, NullBloomStorage.Instance, _logManager);
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
        
        PoSSwitcher poSSwitcher = new (new MergeConfig{Enabled = true, TerminalTotalDifficulty = "1000000"},
            dbProvider.MetadataDb, BlockTree, specProvider, LimboLogs.Instance);
        TotalDifficultyBasedBetterPeerStrategy preMergePeerStrategy = new(syncProgressResolver, LimboLogs.Instance);
        MergeBetterPeerStrategy mergePeerStrategy =
            new(preMergePeerStrategy, syncProgressResolver, poSSwitcher, LimboLogs.Instance);
        MultiSyncModeSelector syncModeSelector = new(syncProgressResolver, SyncPeerPool,
            syncConfig, No.BeaconSync, mergePeerStrategy, _logManager);
        BeaconPivot pivot = new(syncConfig, new MergeConfig(), dbProvider.MetadataDb, BlockTree, new PeerRefresher(SyncPeerPool), LimboLogs.Instance);
        MergeBlockDownloaderFactory blockDownloaderFactory = new(
            poSSwitcher,
            pivot,
            MainnetSpecProvider.Instance,
            BlockTree,
            NullReceiptStorage.Instance,
            Always.Valid,
            Always.Valid,
            SyncPeerPool,
            stats,
            syncModeSelector,
            syncConfig,
            mergePeerStrategy,
            _logManager);
        Synchronizer = new MergeSynchronizer(
            dbProvider,
            specProvider,
            BlockTree,
            NullReceiptStorage.Instance,
            SyncPeerPool,
            stats,
            syncModeSelector,
            syncConfig,
            blockDownloaderFactory,
            pivot,
            new MergeConfig(),
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
