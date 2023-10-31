// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Synchronization.FastBlocks;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test.FastBlocks
{
    [Parallelizable(ParallelScope.Self)]
    [TestFixture]
    public class SyncStatusListTests
    {
        [Test]
        public void Out_of_range_access_throws()
        {
            FastBlockStatusList list = new(1);

            FastBlockStatus a = list[0];
            list.TrySet(0, a);

            Assert.Throws<IndexOutOfRangeException>(() => { FastBlockStatus a = list[-1]; });
            Assert.Throws<IndexOutOfRangeException>(() => { FastBlockStatus a = list[1]; });
            Assert.Throws<IndexOutOfRangeException>(() => { list.TrySet(-1, FastBlockStatus.BodiesPending); });
            Assert.Throws<IndexOutOfRangeException>(() => { list.TrySet(1, FastBlockStatus.BodiesPending); });
        }

        [Test]
        public void Can_read_back_all_set_values()
        {
            const int length = 4096;

            FastBlockStatusList list = CreateFastBlockStatusList(length, false);
            for (int i = 0; i < length; i++)
            {
                Assert.IsTrue((FastBlockStatus)(i % 5) == list[i]);
            }
        }

        [Test]
        public void Will_not_go_below_ancient_barrier()
        {
            IBlockTree blockTree = Substitute.For<IBlockTree>();
            blockTree.FindCanonicalBlockInfo(Arg.Any<long>()).Returns(new BlockInfo(TestItem.KeccakA, 0));
            SyncStatusList syncStatusList = new TestSyncStatusList(blockTree, 1000, null, 900);
            syncStatusList.Reset();

            BlockInfo?[] infos = new BlockInfo?[500];
            syncStatusList.GetInfosForBatch(infos);

            infos.Count((it) => it != null).Should().Be(101);
        }

        [Test]
        public void Can_read_back_all_parallel_set_values()
        {
            const long length = 4096;

            for (var len = 0; len < length; len++)
            {
                FastBlockStatusList list = CreateFastBlockStatusList(len);
                Parallel.For(0, len, (i) =>
                {
                    Assert.IsTrue((FastBlockStatus)(i % 5) == list[i]);
                });
            }
        }

        [Test]
        public void State_transitions_are_enforced()
        {
            const long length = 4096;

            for (var len = 0; len < length; len++)
            {
                FastBlockStatusList list = CreateFastBlockStatusList(len, false);
                for (int i = 0; i < len; i++)
                {
                    AssertTransition(list, i);
                }
            }
        }

        [Test]
        public void State_transitions_are_enforced_in_parallel()
        {
            const long length = 4096;

            for (var len = 0; len < length; len++)
            {
                FastBlockStatusList list = CreateFastBlockStatusList(len);
                Parallel.For(0, len, (i) =>
                {
                    AssertTransition(list, i);
                });
            }
        }


        private static void AssertTransition(FastBlockStatusList list, int i)
        {
            switch (list[i])
            {
                case FastBlockStatus.BodiesPending:
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesPending));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesInserted));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptInserted));

                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.BodiesRequestSent));
                    break;

                case FastBlockStatus.BodiesRequestSent:
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptInserted));

                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.BodiesInserted));
                    list[i] = FastBlockStatus.BodiesRequestSent;
                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.BodiesPending));
                    break;

                case FastBlockStatus.BodiesInserted:
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesPending));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesInserted));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptInserted));

                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.ReceiptRequestSent));
                    break;

                case FastBlockStatus.ReceiptRequestSent:
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesPending));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptRequestSent));

                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.BodiesInserted));
                    list[i] = FastBlockStatus.ReceiptRequestSent;
                    Assert.IsTrue(list.TrySet(i, FastBlockStatus.ReceiptInserted));
                    break;

                case FastBlockStatus.ReceiptInserted:
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesPending));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.BodiesInserted));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptRequestSent));
                    Assert.IsFalse(list.TrySet(i, FastBlockStatus.ReceiptInserted));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static FastBlockStatusList CreateFastBlockStatusList(int length, bool parallel = true) =>
            new(Enumerable.Range(0, length).Select(i => (FastBlockStatus)(i % 5)).ToList(), parallel);

        private class TestSyncStatusList: SyncStatusList
        {
            private readonly long _pivot;
            private readonly long? _lowestInserted;
            private readonly long _barrier;


            public TestSyncStatusList(IBlockTree blockTree, long pivot, long? lowestInserted, long barrier) : base(blockTree)
            {
                _pivot = pivot;
                _lowestInserted = lowestInserted;
                _barrier = barrier;
            }

            public override void Reset()
            {
                base.Reset(_pivot, _lowestInserted, _barrier);
            }
        }
    }
}
