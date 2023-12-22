// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using FluentAssertions;
using Nethermind.Core.Crypto;
using Nethermind.Db.Rocks;
using Nethermind.Int256;

namespace Nethermind.Verkle.Tree.Test;

public class LinkedListCacheTest
{
    [TearDown]
    public void CleanTestData()
    {
        string dbPath = VerkleTestUtils.GetDbPathForTest();
        if (Directory.Exists(dbPath))
        {
            Directory.Delete(dbPath, true);
        }
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGetMultiBlockReverseState(DbMode dbMode)
    {
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(dbMode, 3);

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, VerkleTestUtils._emptyArray);
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, VerkleTestUtils._emptyArray);
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, VerkleTestUtils._emptyArray);
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, VerkleTestUtils._emptyArray);
        tree.Commit();
        tree.CommitTree(0);
        Hash256 stateRoot0 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)1).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)1).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)1).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)1).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)1).ToBigEndian());
        tree.Commit();
        tree.CommitTree(1);
        Hash256 stateRoot1 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)2).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)2).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)2).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)2).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)2).ToBigEndian());
        tree.Commit();
        tree.CommitTree(2);
        Hash256 stateRoot2 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)2).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)2).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)2).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)2).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)2).ToBigEndian());

        tree.StateRoot = stateRoot1;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)1).ToBigEndian());

        tree.StateRoot = stateRoot0;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);

        tree.StateRoot = stateRoot2;

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)3).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)3).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)3).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)3).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)3).ToBigEndian());
        tree.Commit();
        tree.CommitTree(3);
        Hash256 stateRoot3 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)3).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)3).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)3).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)3).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)3).ToBigEndian());

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)4).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)4).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)4).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)4).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)4).ToBigEndian());
        tree.Commit();
        tree.CommitTree(4);
        Hash256 stateRoot4 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)5).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)5).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)5).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)5).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)5).ToBigEndian());
        tree.Commit();
        tree.CommitTree(5);
        Hash256 stateRoot5 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());

        tree.Insert((Hash256)VerkleTestUtils._keyVersion, ((UInt256)6).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyBalance, ((UInt256)6).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyNonce, ((UInt256)6).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeCommitment, ((UInt256)6).ToBigEndian());
        tree.Insert((Hash256)VerkleTestUtils._keyCodeSize, ((UInt256)6).ToBigEndian());
        tree.Commit();
        tree.CommitTree(6);
        Hash256 stateRoot6 = tree.StateRoot;

        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)6).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)6).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)6).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)6).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)6).ToBigEndian());

        tree.StateRoot = stateRoot4;
        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)4).ToBigEndian());

        tree.StateRoot = stateRoot5;
        tree.Get(VerkleTestUtils._keyVersion.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyBalance.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyNonce.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeCommitment.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
        tree.Get(VerkleTestUtils._keyCodeSize.AsSpan()).Should().BeEquivalentTo(((UInt256)5).ToBigEndian());
    }

}
