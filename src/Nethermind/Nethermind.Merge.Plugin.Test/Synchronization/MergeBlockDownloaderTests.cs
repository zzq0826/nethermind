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
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Consensus;
using Nethermind.Consensus.Validators;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.Synchronization;
using Nethermind.Specs;
using Nethermind.Synchronization;
using Nethermind.Synchronization.Blocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.Reporting;
using Nethermind.Synchronization.Test;
using Nethermind.Synchronization.Test.Mocks;
using Nethermind.Trie.Pruning;
using NSubstitute;
using NUnit.Framework;
using No = Nethermind.Synchronization.No;

namespace Nethermind.Merge.Plugin.Test.Synchronization;

public class MergeBlockDownloaderTests
{
    [TestCase(32L, DownloaderOptions.Process, 32, 32)]
    [TestCase(32L, DownloaderOptions.Process, 32, 29)]
    public async Task Merge_Happy_path(long headNumber, int options, int threshold, long insertedBeaconBlocks)
    {
        Context ctx = new(16, 4, headNumber, insertedBeaconBlocks);
        DownloaderOptions downloaderOptions = (DownloaderOptions)options;
        bool withReceipts = downloaderOptions == DownloaderOptions.WithReceipts;
        InMemoryReceiptStorage receiptStorage = new();
        MergeBlockDownloader downloader = new(ctx.PosSwitcher, ctx.BeaconPivot, ctx.Feed, ctx.PeerPool, ctx.NotSyncedTree,
            Always.Valid, Always.Valid, NullSyncReport.Instance, receiptStorage, RopstenSpecProvider.Instance,
            CreateMergePeerChoiceStrategy(ctx.PosSwitcher), new ChainLevelHelper(ctx.NotSyncedTree, ctx.SyncConfig, LimboLogs.Instance),
            LimboLogs.Instance);

        SyncResponse responseOptions = SyncResponse.AllCorrect;
        if (withReceipts)
        {
            responseOptions |= SyncResponse.WithTransactions;
        }

        BlockDownloaderSyncPeerMock synchronizerSyncPeer = new(ctx.SyncedTree, withReceipts, responseOptions);
        PeerInfo peerInfo = new(synchronizerSyncPeer);
        await downloader.DownloadBlocks(peerInfo, new BlocksRequest(downloaderOptions), CancellationToken.None);
        ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(Math.Max(0, insertedBeaconBlocks));
        ctx.BlockTree.BestSuggestedBody.Number.Should().Be(Math.Max(0, insertedBeaconBlocks));
        ctx.BlockTree.IsMainChain(ctx.BlockTree.BestSuggestedHeader.Hash).Should()
            .Be(downloaderOptions != DownloaderOptions.Process);

        int receiptCount = 0;
        for (int i = (int)Math.Max(0, headNumber - threshold); i < peerInfo.HeadNumber; i++)
        {
            if (i % 3 == 0)
            {
                receiptCount += 2;
            }
        }

        receiptStorage.Count.Should().Be(withReceipts ? receiptCount : 0);
    }

    private IBetterPeerStrategy CreateMergePeerChoiceStrategy(IPoSSwitcher poSSwitcher)
    {
        ISyncProgressResolver syncProgressResolver = Substitute.For<ISyncProgressResolver>();
        TotalDifficultyBasedBetterPeerStrategy preMergePeerStrategy = new(syncProgressResolver, LimboLogs.Instance);
        return new MergeBetterPeerStrategy(preMergePeerStrategy, syncProgressResolver, poSSwitcher, LimboLogs.Instance);
    }

    private class Context
    {
        public IBlockTree BlockTree;
        public ISyncPeerPool PeerPool;
        public ISyncFeed<BlocksRequest> Feed;
        public IPoSSwitcher PosSwitcher;
        public IBeaconPivot BeaconPivot;
        public IBlockTree SyncedTree;
        public IBlockTree NotSyncedTree;
        public ISyncConfig SyncConfig;
        public BlockTreeTests.BlockTreeTestScenario.ScenarioBuilder blockTrees;

        public Context(int pivotNumber, int nonSyncedTreeSize, long headNumber, long insertedBeaconBlocks)
        {
            BlockTreeTests.BlockTreeTestScenario.ScenarioBuilder blockTrees = BlockTreeTests.BlockTreeTestScenario
                .GoesLikeThis()
                .WithBlockTrees(nonSyncedTreeSize, (int)headNumber + 1)
                .InsertBeaconPivot(pivotNumber)
                .InsertHeaders(nonSyncedTreeSize, pivotNumber)
                .InsertBeaconBlocks(pivotNumber, insertedBeaconBlocks, true);
            NotSyncedTree = blockTrees.NotSyncedTree;
            SyncedTree = blockTrees.SyncedTree;
            
            BlockTree = NotSyncedTree;
            PeerPool = Substitute.For<ISyncPeerPool>();
            Feed = Substitute.For<ISyncFeed<BlocksRequest>>();

            MemDb stateDb = new();

            SyncConfig = new SyncConfig();
            SyncProgressResolver syncProgressResolver = new(
                BlockTree,
                NullReceiptStorage.Instance,
                stateDb,
                new TrieStore(stateDb, LimboLogs.Instance),
                SyncConfig,
                LimboLogs.Instance);
            TotalDifficultyBasedBetterPeerStrategy bestPeerStrategy =
                new(syncProgressResolver, LimboLogs.Instance);
            ISyncModeSelector syncModeSelector = new MultiSyncModeSelector(syncProgressResolver, PeerPool, SyncConfig, No.BeaconSync,
                bestPeerStrategy, LimboLogs.Instance);
            Feed = new FullSyncFeed(syncModeSelector, LimboLogs.Instance);
            MergeConfig mergeConfig = new() { Enabled = true };
            MemDb metadataDb = blockTrees.NotSyncedTreeBuilder.MetadataDb;
            PosSwitcher = new PoSSwitcher(new MergeConfig() { Enabled = true }, metadataDb, NotSyncedTree,
                RopstenSpecProvider.Instance, LimboLogs.Instance);
            BeaconPivot = new BeaconPivot(new SyncConfig(), mergeConfig, metadataDb, NotSyncedTree,
                new PeerRefresher(Substitute.For<ISyncPeerPool>()), LimboLogs.Instance);
            BeaconPivot.EnsurePivot(SyncedTree.FindHeader(pivotNumber, BlockTreeLookupOptions.None));
        }
    }
}
