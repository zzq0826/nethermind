// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Synchronization.VerkleSync;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Proofs;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test.VerkleSync
{
    [TestFixture]
    public class RecreateVerkleStateFromAccountRangesTests
    {
        private VerkleStateTree _inputTree;

        [OneTimeSetUp]
        public void Setup()
        {
            _inputTree = TestItem.Tree.GetVerkleStateTree(null);
        }

        Dictionary<byte[], (byte, byte[])[]> subTreesDict06 = new()
        {
            [TestItem.Tree.stem0] = TestItem.Tree._account0.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem1] = TestItem.Tree._account1.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem2] = TestItem.Tree._account2.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem3] = TestItem.Tree._account3.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem4] = TestItem.Tree._account4.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem5] = TestItem.Tree._account5.ToVerkleDict().ToArray()
        };

        Dictionary<byte[], (byte, byte[])[]> subTreesDict03 = new()
        {
            [TestItem.Tree.stem0] = TestItem.Tree._account0.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem1] = TestItem.Tree._account1.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem2] = TestItem.Tree._account2.ToVerkleDict().ToArray()
        };

        Dictionary<byte[], (byte, byte[])[]> subTreesDict24 = new()
        {
            [TestItem.Tree.stem2] = TestItem.Tree._account2.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem3] = TestItem.Tree._account3.ToVerkleDict().ToArray()
        };

        Dictionary<byte[], (byte, byte[])[]> subTreesDict35 = new()
        {
            [TestItem.Tree.stem3] = TestItem.Tree._account3.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem4] = TestItem.Tree._account4.ToVerkleDict().ToArray()
        };

        Dictionary<byte[], (byte, byte[])[]> subTreesDict46 = new()
        {
            [TestItem.Tree.stem4] = TestItem.Tree._account4.ToVerkleDict().ToArray(),
            [TestItem.Tree.stem5] = TestItem.Tree._account5.ToVerkleDict().ToArray()
        };


        [Test]
        public void RecreateAccountStateFromOneRangeWithNonExistenceProof()
        {
            Dictionary<byte[], (byte, byte[])[]> subTreesDict = TestItem.Tree.GetSubTreeDict();
            VerkleProof proof =
                _inputTree.CreateVerkleRangeProof(Keccak.Zero.Bytes[..31], TestItem.Tree.stem5, out Banderwagon rootPoint);

            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);
            AddRangeResult result = verkleSyncProvider.AddSubTreeRange(1, rootPoint, Keccak.Zero.Bytes[..31], subTreesDict, proof, TestItem.Tree.stem5);
            Assert.That(result, Is.EqualTo(AddRangeResult.OK));

            Console.WriteLine(dbProvider.InternalNodesDb.GetSize());
        }

        [Test]
        public void RecreateAccountStateFromOneRangeWithExistenceProof()
        {
            Dictionary<byte[], (byte, byte[])[]> subTreesDict = TestItem.Tree.GetSubTreeDict();
            VerkleProof proof =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem0, TestItem.Tree.stem5, out Banderwagon rootPoint);


            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);
            AddRangeResult result = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem0, subTreesDict, proof, TestItem.Tree.stem5);
            Assert.That(result, Is.EqualTo(AddRangeResult.OK));
        }

        [Test]
        public void RecreateAccountStateFromMultipleRange()
        {
            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);
            Dictionary<byte[], (byte, byte[])[]> subTreesDict1 = TestItem.Tree.GetSubTreeDict1();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict2 = TestItem.Tree.GetSubTreeDict2();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict3 = TestItem.Tree.GetSubTreeDict3();

            VerkleProof proof1 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem0, TestItem.Tree.stem1, out Banderwagon rootPoint);
            VerkleProof proof2 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem2, TestItem.Tree.stem3, out rootPoint);
            VerkleProof proof3 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem4, TestItem.Tree.stem5, out rootPoint);

            AddRangeResult result1 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem0, subTreesDict1, proof1, TestItem.Tree.stem1);
            Assert.That(result1, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result2 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem2, subTreesDict2, proof2, TestItem.Tree.stem3);
            Assert.That(result2, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result3 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem4, subTreesDict3, proof3, TestItem.Tree.stem5);
            Assert.That(result3, Is.EqualTo(AddRangeResult.OK));
        }

        [Test]
        public void RecreateAccountStateFromMultipleRange_InReverseOrder()
        {
            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);
            Dictionary<byte[], (byte, byte[])[]> subTreesDict1 = TestItem.Tree.GetSubTreeDict1();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict2 = TestItem.Tree.GetSubTreeDict2();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict3 = TestItem.Tree.GetSubTreeDict3();

            VerkleProof proof1 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem0, TestItem.Tree.stem1, out Banderwagon rootPoint);
            VerkleProof proof2 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem2, TestItem.Tree.stem3, out rootPoint);
            VerkleProof proof3 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem4, TestItem.Tree.stem5, out rootPoint);

            AddRangeResult result1 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem4, subTreesDict3, proof3, TestItem.Tree.stem5);
            Assert.That(result1, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result2 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem2, subTreesDict2, proof2, TestItem.Tree.stem3);
            Assert.That(result2, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result3 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem0, subTreesDict1, proof1, TestItem.Tree.stem1);
            Assert.That(result3, Is.EqualTo(AddRangeResult.OK));
        }

        [Test]
        public void RecreateAccountStateFromMultipleRange_OutOfOrder()
        {
            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);
            Dictionary<byte[], (byte, byte[])[]> subTreesDict1 = TestItem.Tree.GetSubTreeDict1();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict2 = TestItem.Tree.GetSubTreeDict2();
            Dictionary<byte[], (byte, byte[])[]> subTreesDict3 = TestItem.Tree.GetSubTreeDict3();

            VerkleProof proof1 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem0, TestItem.Tree.stem1, out Banderwagon rootPoint);
            VerkleProof proof2 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem2, TestItem.Tree.stem3, out rootPoint);
            VerkleProof proof3 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem4, TestItem.Tree.stem5, out rootPoint);

            AddRangeResult result2 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem2, subTreesDict2, proof2, TestItem.Tree.stem3);
            Assert.That(result2, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result1 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem0, subTreesDict1, proof1, TestItem.Tree.stem1);
            Assert.That(result1, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result3 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem4, subTreesDict3, proof3, TestItem.Tree.stem5);
            Assert.That(result3, Is.EqualTo(AddRangeResult.OK));
        }

        [Test]
        public void RecreateAccountStateFromMultipleOverlappingRange()
        {
            IDbProvider dbProvider = VerkleDbFactory.InitDatabase(DbMode.MemDb, null);
            VerkleProgressTracker progressTracker = new(null, dbProvider.GetDb<IDb>(DbNames.State), LimboLogs.Instance);
            VerkleSyncProvider verkleSyncProvider = new(progressTracker, dbProvider, LimboLogs.Instance);

            VerkleProof proof1 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem0, TestItem.Tree.stem2, out Banderwagon rootPoint);
            VerkleProof proof2 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem2, TestItem.Tree.stem3, out rootPoint);
            VerkleProof proof3 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem3, TestItem.Tree.stem4, out rootPoint);
            VerkleProof proof4 =
                _inputTree.CreateVerkleRangeProof(TestItem.Tree.stem4, TestItem.Tree.stem5, out rootPoint);

            AddRangeResult result2 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem0, subTreesDict03, proof1, TestItem.Tree.stem2);
            Assert.That(result2, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result1 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem2, subTreesDict24, proof2, TestItem.Tree.stem3);
            Assert.That(result1, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result3 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem3, subTreesDict35, proof3, TestItem.Tree.stem4);
            Assert.That(result3, Is.EqualTo(AddRangeResult.OK));

            AddRangeResult result4 = verkleSyncProvider.AddSubTreeRange(1, rootPoint, TestItem.Tree.stem4, subTreesDict46, proof4, TestItem.Tree.stem5);
            Assert.That(result4, Is.EqualTo(AddRangeResult.OK));
        }
    }
}
