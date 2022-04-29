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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.Network;
using Nethermind.Network.P2P.Subprotocols.Eth.V62;
using Nethermind.Network.P2P.Subprotocols.Eth.V63;
using Nethermind.Specs;
using Nethermind.State.Proofs;
using Nethermind.State.Repositories;
using Nethermind.Stats.Model;
using Nethermind.Db.Blooms;
using Nethermind.Network.P2P.Subprotocols.Eth.V62.Messages;
using Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages;
using Nethermind.Specs.Forks;
using Nethermind.Synchronization.Blocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.Peers;
using Nethermind.Synchronization.Reporting;
using Nethermind.Synchronization.Test.Mocks;
using Nethermind.Trie.Pruning;
using Nethermind.TxPool;
using NSubstitute;
using NSubstitute.Exceptions;
using NUnit.Framework;
using BlockTree = Nethermind.Blockchain.BlockTree;

namespace Nethermind.Synchronization.Test
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public partial class BlockDownloaderTests
    {
        [TestCase(1L, DownloaderOptions.Process, 0)]
        [TestCase(32L, DownloaderOptions.Process, 0)]
        [TestCase(32L, DownloaderOptions.None, 0)]
        [TestCase(1L, DownloaderOptions.WithReceipts, 0)]
        [TestCase(2L, DownloaderOptions.WithReceipts, 0)]
        [TestCase(3L, DownloaderOptions.WithReceipts, 0)]
        [TestCase(32L, DownloaderOptions.WithReceipts, 0)]
        [TestCase(SyncBatchSize.Max * 8, DownloaderOptions.WithReceipts, 0)]
        [TestCase(SyncBatchSize.Max * 8, DownloaderOptions.Process, 0)]
        [TestCase(1L, DownloaderOptions.Process, 32)]
        [TestCase(32L, DownloaderOptions.Process, 32)]
        [TestCase(32L, DownloaderOptions.None, 32)]
        [TestCase(1L, DownloaderOptions.WithReceipts, 32)]
        [TestCase(2L, DownloaderOptions.WithReceipts, 32)]
        [TestCase(3L, DownloaderOptions.WithReceipts, 32)]
        [TestCase(32L, DownloaderOptions.WithReceipts, 32)]
        [TestCase(SyncBatchSize.Max * 8, DownloaderOptions.WithReceipts, 32)]
        [TestCase(SyncBatchSize.Max * 8, DownloaderOptions.Process, 32)]
        public async Task Happy_path(long headNumber, int options, int threshold)
        {
            Context ctx = new();
            DownloaderOptions downloaderOptions = (DownloaderOptions)options;
            bool withReceipts = downloaderOptions == DownloaderOptions.WithReceipts;
            InMemoryReceiptStorage receiptStorage = new();
            BlockDownloader downloader = new(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Valid, Always.Valid,
                NullSyncReport.Instance, receiptStorage, RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);

            SyncResponse syncResponseOptions = SyncResponse.AllCorrect;
            if (withReceipts)
            {
                syncResponseOptions |= SyncResponse.WithTransactions;
            }

            // normally chain length should be head number + 1 so here we setup a slightly shorter chain which
            // will only be fixed slightly later
            long chainLength = headNumber + 1;
            BlockDownloaderSyncPeerMock syncPeer = new(chainLength, withReceipts, syncResponseOptions);

            PeerInfo peerInfo = new(syncPeer);

            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.None, threshold),
                CancellationToken.None);
            ctx.BlockTree.BestSuggestedHeader.Number.Should()
                .Be(Math.Max(0, Math.Min(headNumber, headNumber - threshold)));

            syncPeer.ExtendTree(chainLength * 2);
            await downloader.DownloadBlocks(peerInfo, new BlocksRequest(downloaderOptions), CancellationToken.None);
            ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(Math.Max(0, peerInfo.HeadNumber));
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

        [Test]
        public async Task Ancestor_lookup_simple()
        {
            Context ctx = new();
            ctx.BlockTree = Build.A.BlockTree().OfChainLength(1024).TestObject;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            SyncResponse blockSyncResponseOptions = SyncResponse.AllCorrect;
            BlockDownloaderSyncPeerMock syncPeer = new(2048 + 1, false, blockSyncResponseOptions);

            PeerInfo peerInfo = new(syncPeer);

            Block block1024 = Build.A.Block.WithParent(ctx.BlockTree.Head)
                .WithDifficulty(ctx.BlockTree.Head.Difficulty + 1).TestObject;
            Block block1025 = Build.A.Block.WithParent(block1024).WithDifficulty(block1024.Difficulty + 1).TestObject;
            Block block1026 = Build.A.Block.WithParent(block1025).WithDifficulty(block1025.Difficulty + 1).TestObject;
            ctx.BlockTree.SuggestBlock(block1024);
            ctx.BlockTree.SuggestBlock(block1025);
            ctx.BlockTree.SuggestBlock(block1026);

            for (int i = 0; i < 1023; i++)
            {
                Assert.AreEqual(ctx.BlockTree.FindBlock(i, BlockTreeLookupOptions.None).Hash,
                    syncPeer.BlockTree.FindBlock(i, BlockTreeLookupOptions.None).Hash, i.ToString());
            }

            await downloader.DownloadBlocks(peerInfo, new BlocksRequest(DownloaderOptions.WithReceipts, 0),
                CancellationToken.None);
            ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(peerInfo.HeadNumber);
            ctx.BlockTree.IsMainChain(ctx.BlockTree.BestSuggestedHeader.Hash).Should().Be(true);
        }

        [Test]
        public async Task Ancestor_lookup_headers()
        {
            Context ctx = new();
            ctx.BlockTree = Build.A.BlockTree().OfChainLength(1024).TestObject;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            SyncResponse syncResponseOptions = SyncResponse.AllCorrect;
            BlockDownloaderSyncPeerMock syncPeer = new(2048 + 1, false, syncResponseOptions);
            PeerInfo peerInfo = new(syncPeer);

            Block block1024 = Build.A.Block.WithParent(ctx.BlockTree.Head)
                .WithDifficulty(ctx.BlockTree.Head.Difficulty + 1).TestObject;
            Block block1025 = Build.A.Block.WithParent(block1024).WithDifficulty(block1024.Difficulty + 1).TestObject;
            Block block1026 = Build.A.Block.WithParent(block1025).WithDifficulty(block1025.Difficulty + 1).TestObject;
            ctx.BlockTree.SuggestBlock(block1024);
            ctx.BlockTree.SuggestBlock(block1025);
            ctx.BlockTree.SuggestBlock(block1026);

            for (int i = 0; i < 1023; i++)
            {
                Assert.AreEqual(ctx.BlockTree.FindBlock(i, BlockTreeLookupOptions.None).Hash,
                    syncPeer.BlockTree.FindBlock(i, BlockTreeLookupOptions.None).Hash, i.ToString());
            }

            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(), CancellationToken.None);
            ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(peerInfo.HeadNumber);
        }

        [Test]
        public void Ancestor_failure()
        {
            Context ctx = new();
            ctx.BlockTree = Build.A.BlockTree().OfChainLength(2048 + 1).TestObject;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            SyncResponse blockSyncResponseOptions = SyncResponse.AllCorrect;
            BlockDownloaderSyncPeerMock syncPeer = new(2072 + 1, true, blockSyncResponseOptions);

            PeerInfo peerInfo = new(syncPeer);

            Assert.ThrowsAsync<EthSyncException>(() =>
                downloader.DownloadHeaders(peerInfo, new BlocksRequest(), CancellationToken.None));
            ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(2048);
        }

        [Test]
        public void Ancestor_failure_blocks()
        {
            Context ctx = new();
            ctx.BlockTree = Build.A.BlockTree().OfChainLength(2048 + 1).TestObject;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            SyncResponse syncResponseOptions = SyncResponse.AllCorrect;
            BlockDownloaderSyncPeerMock syncPeer = new(2072 + 1, true, syncResponseOptions);

            PeerInfo peerInfo = new(syncPeer);

            Assert.ThrowsAsync<EthSyncException>(() =>
                downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None));
            ctx.BlockTree.BestSuggestedHeader.Number.Should().Be(2048);
        }

        [TestCase(32)]
        [TestCase(1)]
        [TestCase(0)]
        public async Task Can_sync_with_peer_when_it_times_out_on_full_batch(int threshold)
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(async ci => await ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                    SyncResponse.AllCorrect | SyncResponse.TimeoutOnFullBatch));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.TimeoutOnFullBatch));

            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.HeadNumber.Returns(SyncBatchSize.Max * 2 + threshold);

            PeerInfo peerInfo = new(syncPeer);

            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, threshold),
                CancellationToken.None).ContinueWith(t => { });
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, threshold),
                CancellationToken.None);
            Assert.AreEqual(Math.Max(0, peerInfo.HeadNumber - threshold), ctx.BlockTree.BestSuggestedHeader.Number);

            syncPeer.HeadNumber.Returns(2 * (SyncBatchSize.Max * 2 + threshold));
            // peerInfo.HeadNumber *= 2;
            await downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None)
                .ContinueWith(t => { });
            await downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None);
            Assert.AreEqual(Math.Max(0, peerInfo.HeadNumber), ctx.BlockTree.BestSuggestedHeader.Number);
        }

        [Test]
        public async Task Headers_already_known()
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                    SyncResponse.AllCorrect | SyncResponse.AllKnown));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                        SyncResponse.AllCorrect | SyncResponse.AllKnown));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(64);

            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                    CancellationToken.None)
                .ContinueWith(t => Assert.True(t.IsCompletedSuccessfully));

            syncPeer.HeadNumber.Returns(128);
            await downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None)
                .ContinueWith(t => Assert.True(t.IsCompletedSuccessfully));
        }

        [TestCase(33L)]
        [TestCase(65L)]
        public async Task Peer_sends_just_one_item_when_advertising_more_blocks(long headNumber)
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1), SyncResponse.AllCorrect));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                        SyncResponse.AllCorrect | SyncResponse.JustFirst));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.HeadNumber.Returns(headNumber);

            Task task = downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None);
            await task.ContinueWith(t => Assert.True(t.IsFaulted));

            Assert.AreEqual(0, ctx.BlockTree.BestSuggestedHeader.Number);
        }

        [TestCase(33L)]
        [TestCase(65L)]
        public async Task Peer_sends_just_one_item_when_advertising_more_blocks_but_no_bodies(long headNumber)
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                    SyncResponse.AllCorrect | SyncResponse.NoBody));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                        SyncResponse.AllCorrect | SyncResponse.JustFirst));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(headNumber);
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);

            Task task = downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None);
            await task.ContinueWith(t => Assert.False(t.IsFaulted));

            Assert.AreEqual(headNumber, ctx.BlockTree.BestSuggestedHeader.Number);
        }

        [Test]
        public async Task Throws_on_null_best_peer()
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);
            Task task1 = downloader.DownloadHeaders(null, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);
            await task1.ContinueWith(t => Assert.True(t.IsFaulted));

            Task task2 = downloader.DownloadBlocks(null, new BlocksRequest(), CancellationToken.None);
            await task2.ContinueWith(t => Assert.True(t.IsFaulted));
        }

        [Test]
        public async Task Throws_on_inconsistent_batch()
        {
            Context ctx = new();
            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                    SyncResponse.AllCorrect ^ SyncResponse.Consistent));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.HeadNumber.Returns(1024);

            BlockDownloader downloader = CreateBlockDownloader(ctx);
            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);
            await task.ContinueWith(t => Assert.True(t.IsFaulted));
        }

        [Test]
        public async Task Throws_on_invalid_seal()
        {
            Context ctx = new();
            BlockDownloader downloader = new(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Valid, Always.Invalid,
                NullSyncReport.Instance, new InMemoryReceiptStorage(), RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1), SyncResponse.AllCorrect));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1000);

            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);
            await task.ContinueWith(t => Assert.True(t.IsFaulted));
        }

        [Test]
        public async Task Throws_on_invalid_header()
        {
            Context ctx = new();
            BlockDownloader downloader = new(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Invalid, Always.Valid,
                NullSyncReport.Instance, new InMemoryReceiptStorage(), RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1), SyncResponse.AllCorrect));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1000);

            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);
            await task.ContinueWith(t => Assert.True(t.IsFaulted));
        }

        private class SlowSealValidator : ISealValidator
        {
            public bool ValidateParams(BlockHeader parent, BlockHeader header)
            {
                Thread.Sleep(1000);
                return true;
            }

            public bool ValidateSeal(BlockHeader header, bool force)
            {
                Thread.Sleep(1000);
                return true;
            }
        }

        private class SlowHeaderValidator : IBlockValidator
        {
            public bool ValidateHash(BlockHeader header)
            {
                Thread.Sleep(1000);
                return true;
            }

            public bool Validate(BlockHeader header, BlockHeader parent, bool isUncle)
            {
                Thread.Sleep(1000);
                return true;
            }

            public bool Validate(BlockHeader header, bool isUncle)
            {
                Thread.Sleep(1000);
                return true;
            }

            public bool ValidateSuggestedBlock(Block block)
            {
                Thread.Sleep(1000);
                return true;
            }

            public bool ValidateProcessedBlock(Block processedBlock, TxReceipt[] receipts, Block suggestedBlock)
            {
                Thread.Sleep(1000);
                return true;
            }
        }

        [Test, MaxTime(7000)]
        [Ignore("Fails OneLoggerLogManager Travis only")]
        public async Task Can_cancel_seal_validation()
        {
            Context ctx = new();
            BlockDownloader downloader = new(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Valid,
                new SlowSealValidator(), NullSyncReport.Instance, new InMemoryReceiptStorage(),
                RopstenSpecProvider.Instance, new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(),
                LimboLogs.Instance);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1), SyncResponse.AllCorrect));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0), SyncResponse.AllCorrect));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.HeadNumber.Returns(1000);

            CancellationTokenSource cancellation = new();
            cancellation.CancelAfter(1000);
            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                cancellation.Token);
            await task.ContinueWith(t => Assert.True(t.IsCanceled, $"headers {t.Status}"));

            // peerInfo.HeadNumber = 2000;
            syncPeer.HeadNumber.Returns(2000);
            cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(1000);
            task = downloader.DownloadBlocks(peerInfo, new BlocksRequest(), cancellation.Token);
            await task.ContinueWith(t => Assert.True(t.IsCanceled, $"blocks {t.Status}"));
        }

        [Test, MaxTime(7000)]
        public async Task Can_cancel_adding_headers()
        {
            Context ctx = new();
            BlockDownloader downloader = new(ctx.Feed, ctx.PeerPool, ctx.BlockTree, new SlowHeaderValidator(),
                Always.Valid, NullSyncReport.Instance, new InMemoryReceiptStorage(), RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1), SyncResponse.AllCorrect));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0), SyncResponse.AllCorrect));

            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.HeadNumber.Returns(1000);

            PeerInfo peerInfo = new(syncPeer);

            CancellationTokenSource cancellation = new();
            cancellation.CancelAfter(1000);
            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                cancellation.Token);
            await task.ContinueWith(t => Assert.True(t.IsCanceled, "headers"));

            syncPeer.HeadNumber.Returns(2000);
            // peerInfo.HeadNumber *= 2;
            cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(1000);
            task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                cancellation.Token);
            await task.ContinueWith(t => Assert.True(t.IsCanceled, "blocks"));
        }

        [Test]
        public async Task Validate_always_the_last_seal_and_random_seal_in_the_package()
        {
            ISealValidator sealValidator = Substitute.For<ISealValidator>();
            sealValidator.ValidateSeal(Arg.Any<BlockHeader>(), Arg.Any<bool>()).Returns(true);
            Context ctx = new();
            BlockDownloader downloader = new BlockDownloader(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Valid,
                sealValidator, NullSyncReport.Instance, new InMemoryReceiptStorage(), RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);
            ;

            BlockHeader[] blockHeaders = await ctx._syncResponseBuilder.BuildHeaderResponse(0, 512, SyncResponse.AllCorrect);
            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(blockHeaders);

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(511);

            Task task = downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);
            await task;

            sealValidator.Received(2).ValidateSeal(Arg.Any<BlockHeader>(), true);
            sealValidator.Received(510).ValidateSeal(Arg.Any<BlockHeader>(), false);
            sealValidator.Received().ValidateSeal(blockHeaders[^1], true);
        }

        private class ThrowingPeer : ISyncPeer
        {
            public ThrowingPeer(long number, UInt256 totalDiff, Keccak headHash = null)
            {
                HeadNumber = number;
                TotalDifficulty = totalDiff;
                HeadHash = headHash ?? Keccak.Zero;
            }

            public Node Node { get; }
            public string ClientId => "EX peer";
            public Keccak HeadHash { get; set; }
            public long HeadNumber { get; set; }
            public UInt256 TotalDifficulty { get; set; } = UInt256.MaxValue;
            public bool IsInitialized { get; set; }

            public void Disconnect(DisconnectReason reason, string details)
            {
                throw new NotImplementedException();
            }

            public Task<BlockBody[]> GetBlockBodies(IReadOnlyList<Keccak> blockHashes, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<BlockHeader[]> GetBlockHeaders(Keccak blockHash, int maxBlocks, int skip,
                CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<BlockHeader[]> GetBlockHeaders(long number, int maxBlocks, int skip, CancellationToken token)
            {
                throw new Exception();
            }

            public Task<BlockHeader> GetHeadBlockHeader(Keccak hash, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public void NotifyOfNewBlock(Block block, SendBlockPriority priority)
            {
                throw new NotImplementedException();
            }

            public PublicKey Id => Node.Id;

            public void SendNewTransactions(IEnumerable<Transaction> txs, bool sendFullTx)
            {
                throw new NotImplementedException();
            }

            public Task<TxReceipt[][]> GetReceipts(IReadOnlyList<Keccak> blockHash, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<byte[][]> GetNodeData(IReadOnlyList<Keccak> hashes, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public void RegisterSatelliteProtocol<T>(string protocol, T protocolHandler) where T : class
            {
                throw new NotImplementedException();
            }

            public bool TryGetSatelliteProtocol<T>(string protocol, out T protocolHandler) where T : class
            {
                throw new NotImplementedException();
            }
        }

        [Test]
        public async Task Faults_on_get_headers_faulting()
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = new ThrowingPeer(1000, UInt256.MaxValue);
            PeerInfo peerInfo = new(syncPeer);

            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                    CancellationToken.None)
                .ContinueWith(t => Assert.True(t.IsFaulted));
        }

        [Test]
        public async Task Throws_on_block_task_exception()
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            Task<BlockHeader[]> buildHeadersResponse = null;
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    buildHeadersResponse =
                        ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                            SyncResponse.AllCorrect));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<BlockBody[]>(new TimeoutException()));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder.BuildReceiptsResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.WithTransactions));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1);
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);

            syncPeer.HeadNumber.Returns(2);

            Func<Task> action = async () =>
                await downloader.DownloadBlocks(peerInfo, new BlocksRequest(), CancellationToken.None);
            await action.Should().ThrowAsync<AggregateException>()
                .WithInnerException<AggregateException, TimeoutException>();
        }

        [TestCase(DownloaderOptions.WithReceipts, true)]
        [TestCase(DownloaderOptions.None, false)]
        [TestCase(DownloaderOptions.Process, false)]
        public async Task Throws_on_receipt_task_exception_when_downloading_receipts(int options, bool shouldThrow)
        {
            Context ctx = new();
            DownloaderOptions downloaderOptions = (DownloaderOptions)options;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);

            Task<BlockHeader[]> buildHeadersResponse = null;
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    buildHeadersResponse =
                        ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                            SyncResponse.AllCorrect));

            Task<BlockBody[]> buildBlocksResponse = null;
            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => buildBlocksResponse = ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.WithTransactions));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<TxReceipt[][]>(new TimeoutException()));

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1);
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, 0),
                CancellationToken.None);

            syncPeer.HeadNumber.Returns(2);
            // peerInfo.HeadNumber *= 2;

            Func<Task> action = async () =>
                await downloader.DownloadBlocks(peerInfo, new BlocksRequest(downloaderOptions), CancellationToken.None);
            if (shouldThrow)
            {
                await action.Should().ThrowAsync<AggregateException>()
                    .WithInnerException<AggregateException, TimeoutException>();
            }
            else
            {
                await action.Should().NotThrowAsync();
            }
        }

        [TestCase(DownloaderOptions.WithReceipts, true)]
        [TestCase(DownloaderOptions.None, false)]
        [TestCase(DownloaderOptions.Process, false)]
        public async Task Throws_on_null_receipt_downloaded(int options, bool shouldThrow)
        {
            Context ctx = new();
            DownloaderOptions downloaderOptions = (DownloaderOptions)options;
            bool withReceipts = downloaderOptions == DownloaderOptions.WithReceipts;
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            SyncResponse syncResponseOptions = SyncResponse.AllCorrect;
            if (withReceipts)
            {
                syncResponseOptions |= SyncResponse.WithTransactions;
            }

            int headNumber = 5;

            // normally chain length should be head number + 1 so here we setup a slightly shorter chain which
            // will only be fixed slightly later
            long chainLength = headNumber + 1;
            BlockDownloaderSyncPeerMock syncPeerInternal = new(chainLength, withReceipts, syncResponseOptions);
            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci => syncPeerInternal.GetBlockHeaders(ci.ArgAt<long>(0), ci.ArgAt<int>(1), ci.ArgAt<int>(2),
                    ci.ArgAt<CancellationToken>(3)));

            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    syncPeerInternal.GetBlockBodies(ci.ArgAt<IReadOnlyList<Keccak>>(0),
                        ci.ArgAt<CancellationToken>(1)));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(async ci =>
                {
                    TxReceipt[][] receipts = await syncPeerInternal.GetReceipts(ci.ArgAt<IReadOnlyList<Keccak>>(0),
                        ci.ArgAt<CancellationToken>(1));
                    receipts[^1] = null;
                    return receipts;
                });

            syncPeer.TotalDifficulty.Returns(ci => syncPeerInternal.TotalDifficulty);
            syncPeer.HeadHash.Returns(ci => syncPeerInternal.HeadHash);
            syncPeer.HeadNumber.Returns(ci => syncPeerInternal.HeadNumber);

            PeerInfo peerInfo = new(syncPeer);

            int threshold = 2;
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.None, threshold),
                CancellationToken.None);
            ctx.BlockTree.BestSuggestedHeader.Number.Should()
                .Be(Math.Max(0, Math.Min(headNumber, headNumber - threshold)));

            syncPeerInternal.ExtendTree(chainLength * 2);
            Func<Task> action = async () =>
                await downloader.DownloadBlocks(peerInfo, new BlocksRequest(downloaderOptions), CancellationToken.None);

            if (shouldThrow)
            {
                await action.Should().ThrowAsync<EthSyncException>();
            }
            else
            {
                await action.Should().NotThrowAsync();
            }
        }

        [TestCase(32)]
        [TestCase(1)]
        [TestCase(0)]
        public async Task Throws_on_block_bodies_count_higher_than_receipts_list_count(int threshold)
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);

            Task<BlockHeader[]> buildHeadersResponse = null;
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    buildHeadersResponse =
                        ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                            SyncResponse.AllCorrect));

            Task<BlockBody[]> buildBlocksResponse = null;
            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => buildBlocksResponse = ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.WithTransactions));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder
                        .BuildReceiptsResponse(ci.ArgAt<IList<Keccak>>(0),
                            SyncResponse.AllCorrect | SyncResponse.WithTransactions).Result.Skip(1).ToArray());

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1);
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, threshold),
                CancellationToken.None);

            syncPeer.HeadNumber.Returns(2);

            Func<Task> action = async () => await downloader.DownloadBlocks(peerInfo,
                new BlocksRequest(DownloaderOptions.WithBodies | DownloaderOptions.WithReceipts),
                CancellationToken.None);
            await action.Should().ThrowAsync<EthSyncException>();
        }

        [TestCase(32)]
        [TestCase(1)]
        public async Task Does_throw_on_transaction_count_different_than_receipts_count_in_block(int threshold)
        {
            Context ctx = new();
            InMemoryReceiptStorage inMemoryReceiptStorage = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);
            Task<BlockHeader[]> buildHeadersResponse = null;
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    buildHeadersResponse =
                        ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                            SyncResponse.AllCorrect));

            Task<BlockBody[]> buildBlocksResponse = null;
            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => buildBlocksResponse = ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.WithTransactions));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => ctx._syncResponseBuilder
                    .BuildReceiptsResponse(ci.ArgAt<IList<Keccak>>(0), SyncResponse.AllCorrect | SyncResponse.WithTransactions)
                    .Result.Select(r => r == null || r.Length == 0 ? r : r.Skip(1).ToArray()).ToArray());

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1);
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.None, threshold),
                CancellationToken.None);

            syncPeer.HeadNumber.Returns(2);

            Func<Task> action = async () => await downloader.DownloadBlocks(peerInfo,
                new BlocksRequest(DownloaderOptions.WithReceipts), CancellationToken.None);
            await action.Should().ThrowAsync<EthSyncException>();
        }

        [TestCase(32)]
        [TestCase(1)]
        public async Task Throws_on_incorrect_receipts_root(int threshold)
        {
            Context ctx = new();
            BlockDownloader downloader = CreateBlockDownloader(ctx);

            ISyncPeer syncPeer = Substitute.For<ISyncPeer>();
            syncPeer.TotalDifficulty.Returns(UInt256.MaxValue);

            Task<BlockHeader[]> buildHeadersResponse = null;
            syncPeer.GetBlockHeaders(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    buildHeadersResponse = ctx._syncResponseBuilder.BuildHeaderResponse(ci.ArgAt<long>(0), ci.ArgAt<int>(1),
                        SyncResponse.IncorrectReceiptRoot));

            Task<BlockBody[]> buildBlocksResponse = null;
            syncPeer.GetBlockBodies(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci => buildBlocksResponse = ctx._syncResponseBuilder.BuildBlocksResponse(ci.ArgAt<IList<Keccak>>(0),
                    SyncResponse.AllCorrect | SyncResponse.WithTransactions));

            syncPeer.GetReceipts(Arg.Any<IReadOnlyList<Keccak>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                    ctx._syncResponseBuilder.BuildReceiptsResponse(ci.ArgAt<IList<Keccak>>(0),
                        SyncResponse.AllCorrect | SyncResponse.WithTransactions).Result);

            PeerInfo peerInfo = new(syncPeer);
            syncPeer.HeadNumber.Returns(1);
            await downloader.DownloadHeaders(peerInfo, new BlocksRequest(DownloaderOptions.WithBodies, threshold),
                CancellationToken.None);

            syncPeer.HeadNumber.Returns(2);

            Func<Task> action = async () => await downloader.DownloadBlocks(peerInfo,
                new BlocksRequest(DownloaderOptions.WithReceipts), CancellationToken.None);
            await action.Should().ThrowAsync<EthSyncException>();
        }

        private BlockDownloader CreateBlockDownloader(Context ctx)
        {
            InMemoryReceiptStorage receiptStorage = new();
            return new BlockDownloader(ctx.Feed, ctx.PeerPool, ctx.BlockTree, Always.Valid, Always.Valid,
                NullSyncReport.Instance, receiptStorage, RopstenSpecProvider.Instance,
                new BlocksSyncPeerAllocationStrategyFactory(), CreatePeerChoiceStrategy(), LimboLogs.Instance);
        }

        private IBetterPeerStrategy CreatePeerChoiceStrategy()
        {
            ISyncProgressResolver syncProgressResolver = Substitute.For<ISyncProgressResolver>();
            return new TotalDifficultyBasedBetterPeerStrategy(syncProgressResolver, LimboLogs.Instance);
        }

        private class Context
        {
            public IBlockTree BlockTree;
            public ISyncPeerPool PeerPool;
            public ISyncFeed<BlocksRequest> Feed;
            public SyncResponseBuilder _syncResponseBuilder;
            public Dictionary<long, Keccak> TestHeaderMapping;
            public ISyncModeSelector SyncModeSelector;

            public Context(BlockTree? blockTree = null)
            {
                Block genesis = Build.A.Block.Genesis.TestObject;
                MemDb blockInfoDb = new();
                BlockTree = blockTree;
                BlockTree ??= new BlockTree(new MemDb(), new MemDb(), blockInfoDb,
                    new ChainLevelInfoRepository(blockInfoDb), MainnetSpecProvider.Instance, NullBloomStorage.Instance,
                    LimboLogs.Instance);
                BlockTree.SuggestBlock(genesis);

                TestHeaderMapping = new Dictionary<long, Keccak>();
                TestHeaderMapping.Add(0, genesis.Hash);

                PeerPool = Substitute.For<ISyncPeerPool>();
                Feed = Substitute.For<ISyncFeed<BlocksRequest>>();

                MemDb stateDb = new();

                SyncConfig syncConfig = new();
                SyncProgressResolver syncProgressResolver = new(
                    BlockTree,
                    NullReceiptStorage.Instance,
                    stateDb,
                    new TrieStore(stateDb, LimboLogs.Instance),
                    syncConfig,
                    LimboLogs.Instance);
                TotalDifficultyBasedBetterPeerStrategy bestPeerStrategy =
                    new (syncProgressResolver, LimboLogs.Instance);
                SyncModeSelector = new MultiSyncModeSelector(syncProgressResolver, PeerPool, syncConfig, No.BeaconSync,
                    bestPeerStrategy, LimboLogs.Instance);
                Feed = new FullSyncFeed(SyncModeSelector, LimboLogs.Instance);

                _syncResponseBuilder = new SyncResponseBuilder(BlockTree, TestHeaderMapping);
            }
        }
    }
}
