// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.TrieStore;

namespace Nethermind.Verkle.Tree.Test;

public class ArchiveStoreTests
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
    public void TestInsertGetMultiBlockReverseStateWithEliasFano(DbMode dbMode)
    {

        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                break;
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, VerkleTestUtils.GetDbPathForTest());
                break;
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }

        VerkleStateStore stateStore = new VerkleStateStore(provider, LimboLogs.Instance, 0);
        VerkleTree tree = new VerkleTree(stateStore, LimboLogs.Instance);

        VerkleArchiveStore archiveStore = new VerkleArchiveStore(stateStore, provider, LimboLogs.Instance);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._emptyArray);
        tree.Commit();
        tree.CommitTree(0);
        VerkleCommitment stateRoot0 = tree.StateRoot;
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyBalance).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyNonce).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        tree.Get(VerkleTestUtils._keyCodeCommitment).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._arrayAll0Last2);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._arrayAll0Last2);
        tree.Commit();
        tree.CommitTree(1);
        VerkleCommitment stateRoot1 = tree.StateRoot;
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyBalance).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyNonce).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        tree.Get(VerkleTestUtils._keyCodeCommitment).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._arrayAll0Last3);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._arrayAll0Last3);
        tree.Commit();
        tree.CommitTree(2);
        Console.WriteLine(tree.StateRoot.ToString());

        tree.Get(VerkleTestUtils._keyVersion).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyBalance).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyNonce).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);
        tree.Get(VerkleTestUtils._keyCodeCommitment).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Get(VerkleTestUtils._keyCodeSize).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last3);

        archiveStore.GetLeaf(VerkleTestUtils._keyVersion, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyBalance, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyNonce, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeCommitment, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeSize, stateRoot1).Should().BeEquivalentTo(VerkleTestUtils._arrayAll0Last2);

        archiveStore.GetLeaf(VerkleTestUtils._keyVersion, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyBalance, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyNonce, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeCommitment, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._valueEmptyCodeHashValue);
        archiveStore.GetLeaf(VerkleTestUtils._keyCodeSize, stateRoot0).Should().BeEquivalentTo(VerkleTestUtils._emptyArray);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestArchiveStoreForMultipleBlocks(DbMode dbMode)
    {
        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                break;
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, VerkleTestUtils.GetDbPathForTest());
                break;
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }

        VerkleStateStore stateStore = new VerkleStateStore(provider, LimboLogs.Instance, 0);
        VerkleTree tree = new VerkleTree(stateStore, LimboLogs.Instance);

        VerkleArchiveStore archiveStore =
            new VerkleArchiveStore(stateStore, provider, LimboLogs.Instance) { BlockChunks = 10 };

        Pedersen[] keys =
        {
            VerkleTestUtils._keyVersion, VerkleTestUtils._keyNonce, VerkleTestUtils._keyBalance,
            VerkleTestUtils._keyCodeSize, VerkleTestUtils._keyCodeCommitment
        };

        List<VerkleCommitment> stateRoots = new List<VerkleCommitment>();
        ulong i = 0;
        long block = 0;
        while (i < 2000)
        {
            foreach (Pedersen key in keys)
            {
                tree.Insert(key, new UInt256(i++).ToBigEndian());
                tree.Commit();
                tree.CommitTree(block++);
                stateRoots.Add(tree.StateRoot);
            }
        }

        i = 0;
        block = 0;
        while (i < 2000)
        {
            foreach (Pedersen key in keys)
            {
                byte[]? leaf = archiveStore.GetLeaf(key.BytesAsSpan, stateRoots[(int)block++]);
                leaf.Should().BeEquivalentTo(new UInt256(i++).ToBigEndian());
            }
        }

    }
}
