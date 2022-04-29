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
using System.Threading;
using Nethermind.Core;
using Nethermind.Synchronization.Blocks;
using Nethermind.Synchronization.Test.Mocks;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test
{
    [TestFixture(SynchronizerType.Fast)]
    [TestFixture(SynchronizerType.Full)]
    [TestFixture(SynchronizerType.Eth2MergeFull)]
    [TestFixture(SynchronizerType.Eth2MergeFast)]
    [TestFixture(SynchronizerType.Eth2MergeFastWithoutTTD)]
    [Parallelizable(ParallelScope.All)]
    public class SynchronizerTests
    {
        private readonly SynchronizerType _synchronizerType;
        
        private const int WaitTime = 500;

        public SynchronizerTests(SynchronizerType synchronizerType)
        {
            _synchronizerType = synchronizerType;
        }

        private WhenImplementation When => new(_synchronizerType);

        private class WhenImplementation
        {
            private readonly SynchronizerType _synchronizerType;

            public WhenImplementation(SynchronizerType synchronizerType)
            {
                _synchronizerType = synchronizerType;
            }

            public SyncingContext Syncing => new(_synchronizerType);
        }
        
        [OneTimeSetUp]
        public void Setup()
        {
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            foreach (SyncingContext syncingContext in SyncingContext.AllInstances)
            {
                syncingContext.Stop();
            }
        }

        [Test, Retry(3)]
        public void Init_condition_are_as_expected()
        {
            When.Syncing
                .AfterProcessingGenesis()
                .BestKnownNumberIs(0)
                .Genesis.BlockIsKnown()
                .BestSuggested.BlockIsSameAsGenesis().Stop();
        }

        [Test, Retry(3)]
        public void Can_sync_with_one_peer_straight()
        {
            SynchronizerSyncPeerMock peerA = new("A");

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggested.BlockIsSameAsGenesis().Stop();
        }

        [Test, Retry(3)]
        public void Can_sync_with_one_peer_straight_and_extend_chain()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(3);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }
        
        [Test, Retry(3)]
        public void Can_extend_chain_by_one_on_new_block_message()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .After(() => peerA.AddBlocksUpTo(2))
                .AfterNewBlockMessage(peerA.HeadBlock, peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_reorg_on_new_block_message()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(3);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(3);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .AfterPeerIsAdded(peerB)
                .WaitUntilInitialized()
                .After(() => peerB.AddBlocksUpTo(6))
                .AfterNewBlockMessage(peerB.HeadBlock, peerB)
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        [Ignore("Not supported for now - still analyzing this scenario")]
        public void Can_reorg_on_hint_block_message()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(3);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(3);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .AfterPeerIsAdded(peerB)
                .Wait()
                .After(() => peerB.AddBlocksUpTo(6))
                .AfterHintBlockMessage(peerB.HeadBlock, peerB)
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_extend_chain_by_one_on_block_hint_message()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .After(() => peerA.AddBlocksUpTo(2))
                .AfterHintBlockMessage(peerA.HeadBlock, peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_extend_chain_by_more_than_one_on_new_block_message()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .After(() => peerA.AddBlocksUpTo(8))
                .AfterNewBlockMessage(peerA.HeadBlock, peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Wait().Stop();

            Console.WriteLine("why?");
        }

        [Test, Retry(3)]
        public void Will_ignore_new_block_that_is_far_ahead()
        {
            // this test was designed for no sync-timer sync process
            // now it checks something different
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .After(() => peerA.AddBlocksUpTo(16))
                .AfterNewBlockMessage(peerA.HeadBlock, peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_sync_when_best_peer_is_timing_out()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            SynchronizerSyncPeerMock badPeer = new("B", false, false, true);
            badPeer.AddBlocksUpTo(20);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(badPeer)
                .WaitUntilInitialized()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedBlockHasNumber(1).Stop();
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void Will_inform_connecting_peer_about_the_alternative_branch_with_same_difficulty()
        {
            if (_synchronizerType == SynchronizerType.Fast)
            {
                return;
            }

            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(2);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(2, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedBlockHasNumber(2)
                .AfterPeerIsAdded(peerB)
                .WaitUntilInitialized()
                .Stop();

            Assert.AreNotEqual(peerB.HeadBlock.Hash, peerA.HeadBlock.Hash);

            Block peerBNewBlock = null;
            SpinWait.SpinUntil(() =>
            {
                bool receivedBlock = peerB.ReceivedBlocks.TryPeek(out peerBNewBlock);
                return receivedBlock && peerBNewBlock.Hash == peerA.HeadBlock.Hash;
            }, WaitTime);

            // if (_synchronizerType == SynchronizerType.Eth2MergeFull)
            // {
            //     Assert.IsNull(peerBNewBlock);
            //     Assert.AreNotEqual(peerB.HeadBlock.Hash, peerA.HeadBlock.Hash);
            // }
            // else
            // {
                Assert.AreEqual(peerBNewBlock?.Header.Hash!, peerA.HeadBlock.Hash);
            // }
        }

        [Test, Retry(3)]
        public void Will_not_add_same_peer_twice()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .PeerCountIs(1)
                .BestSuggestedBlockHasNumber(1).Stop();
        }

        [Test, Retry(3)]
        public void Will_remove_peer_when_init_fails()
        {
            SynchronizerSyncPeerMock peerA = new("A", true, true);
            peerA.AddBlocksUpTo(1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitAMoment()
                .PeerCountIs(0).Stop();
        }


        [Test, Retry(3)]
        public void Can_remove_peers()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            SynchronizerSyncPeerMock peerB = new("B");

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .AfterPeerIsAdded(peerB)
                .WaitAMoment()
                .PeerCountIs(2)
                .AfterPeerIsRemoved(peerB)
                .WaitAMoment()
                .PeerCountIs(1)
                .AfterPeerIsRemoved(peerA)
                .WaitAMoment()
                .PeerCountIs(0).Stop();
        }

        [Test, Retry(3)]
        public void Can_reorg_on_add_peer()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(SyncBatchSize.Max);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(SyncBatchSize.Max * 2, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader)
                .AfterPeerIsAdded(peerB)
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_reorg_based_on_total_difficulty()
        {
            if (_synchronizerType == SynchronizerType.Eth2MergeFastWithoutTTD)
            {
                return;
            }
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(10);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddHighDifficultyBlocksUpTo(6, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader)
                .AfterPeerIsAdded(peerB)
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        [Ignore("Not supported for now - still analyzing this scenario")]
        public void Can_extend_chain_on_hint_block_when_high_difficulty_low_number()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(10);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddHighDifficultyBlocksUpTo(5, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .Wait()
                .AfterPeerIsAdded(peerB)
                .Wait()
                .After(() => peerB.AddHighDifficultyBlocksUpTo(6, 0, 1))
                .AfterHintBlockMessage(peerB.HeadBlock, peerB)
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_extend_chain_on_new_block_when_high_difficulty_low_number()
        {
            if (_synchronizerType == SynchronizerType.Eth2MergeFastWithoutTTD)
            {
                return;
            }
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(10);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddHighDifficultyBlocksUpTo(6, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .WaitUntilInitialized()
                .AfterPeerIsAdded(peerB)
                .WaitUntilInitialized()
                .After(() => peerB.AddHighDifficultyBlocksUpTo(6, 0, 1))
                .AfterNewBlockMessage(peerB.HeadBlock, peerB)
                .WaitUntilInitialized()
                .BestSuggestedHeaderIs(peerB.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Will_not_reorganize_on_same_chain_length()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(10);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(10, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader)
                .AfterPeerIsAdded(peerB)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Will_not_reorganize_more_than_max_reorg_length()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(BlockDownloader.MaxReorganizationLength + 1);

            SynchronizerSyncPeerMock peerB = new("B");
            peerB.AddBlocksUpTo(BlockDownloader.MaxReorganizationLength + 2, 0, 1);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader)
                .AfterPeerIsAdded(peerB)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Ignore("travis")]
        public void Can_sync_more_than_a_batch()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(SyncBatchSize.Max * 3);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader).Stop();
        }

        [Test, Retry(3)]
        public void Can_sync_exactly_one_batch()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(SyncBatchSize.Max);

            When.Syncing
                .AfterProcessingGenesis()
                .AfterPeerIsAdded(peerA)
                .BestSuggestedHeaderIs(peerA.HeadHeader)
                .Stop();
        }

        [Test, Retry(3)]
        public void Can_stop()
        {
            SynchronizerSyncPeerMock peerA = new("A");
            peerA.AddBlocksUpTo(SyncBatchSize.Max);

            When.Syncing
                .Stop();
        }
    }
}
