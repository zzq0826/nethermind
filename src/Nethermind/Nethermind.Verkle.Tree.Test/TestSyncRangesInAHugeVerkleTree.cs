// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Core.Verkle;
using Nethermind.Db.Rocks;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TrieStore;

namespace Nethermind.Verkle.Tree.Test;

public class TestSyncRangesInAHugeVerkleTree
{
    public static Random Random { get; } = new(0);
    public static int numKeys = 2000;

    [TearDown]
    public void CleanTestData()
    {
        string dbPath = VerkleTestUtils.GetDbPathForTest();
        if (Directory.Exists(dbPath))
        {
            Directory.Delete(dbPath, true);
        }
    }

    private Hash256[] GetPedersenPathPool(int pathPoolCount)
    {
        Hash256[] pathPool = new Hash256[pathPoolCount];
        for (int i = 0; i < pathPoolCount; i++)
        {
            byte[] key = new byte[32];
            ((UInt256)i).ToBigEndian(key);
            Hash256 keccak = new Hash256(key);
            pathPool[i] = keccak;
        }
        return pathPool;
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void GetSyncRangeForBigVerkleTree(DbMode dbMode)
    {
        const int pathPoolCount = 100_000;
        const int numBlocks = 200;
        const int leafPerBlock = 10;
        const int blockToGetIteratorFrom = 180;
        const int initialLeafCount = 10000;

        IVerkleTrieStore store = TestItem.GetVerkleStore(dbMode);
        VerkleTree tree = new(store, LimboLogs.Instance);

        Hash256[] pathPool = GetPedersenPathPool(pathPoolCount);
        SortedDictionary<Hash256, byte[]> leafs = new();
        SortedDictionary<Hash256, byte[]> leafsForSync = new();

        for (int leafIndex = 0; leafIndex < initialLeafCount; leafIndex++)
        {
            byte[] value = new byte[32];
            Random.NextBytes(value);
            Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];
            tree.Insert(path, value);
            leafs[path] = value;
            leafsForSync[path] = value;
        }

        tree.Commit();
        tree.CommitTree(0);


        Hash256 stateRoot180 = Pedersen.Zero;
        for (int blockNumber = 1; blockNumber <= numBlocks; blockNumber++)
        {
            for (int accountIndex = 0; accountIndex < leafPerBlock; accountIndex++)
            {
                byte[] leafValue = new byte[32];

                Random.NextBytes(leafValue);
                Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];

                if (leafs.ContainsKey(path))
                {
                    if (!(Random.NextSingle() > 0.5)) continue;
                    // Console.WriteLine($"blockNumber:{blockNumber} uKey:{path} uValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    if(blockToGetIteratorFrom >= blockNumber) leafsForSync[path] = leafValue;
                    // Console.WriteLine("new values");
                }
                else
                {
                    // Console.WriteLine($"blockNumber:{blockNumber} nKey:{path} nValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    if(blockToGetIteratorFrom >= blockNumber) leafsForSync[path] = leafValue;
                }
            }

            tree.Commit();
            tree.CommitTree(blockNumber);
            if (blockNumber == blockToGetIteratorFrom) stateRoot180 = tree.StateRoot;
        }


        Hash256[] keysArray = leafs.Keys.ToArray();
        int keyLength = keysArray.Length;
        using IEnumerator<KeyValuePair<byte[], byte[]>> rangeEnum =
            tree._verkleStateStore
                .GetLeafRangeIterator(
                keysArray[keyLength/4].Bytes.ToArray(),
                keysArray[(keyLength*2)/3].Bytes.ToArray(), 180)
                .GetEnumerator();


        while (rangeEnum.MoveNext())
        {
            // Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} AcValue:{rangeEnum.Current.Value.ToHexString()} ExValue:{leafsForSync[rangeEnum.Current.Key].ToHexString()}");
            Assert.That(rangeEnum.Current.Value.SequenceEqual(leafsForSync[(Hash256)rangeEnum.Current.Key]), Is.True);
        }

        using IEnumerator<PathWithSubTree> rangeEnumSized =
            tree._verkleStateStore
                .GetLeafRangeIterator(
                    keysArray[keyLength/4].Bytes.Slice(0,31).ToArray(),
                    keysArray[(keyLength*2)/3].Bytes.Slice(0,31).ToArray(), stateRoot180, 1000)
                .GetEnumerator();


        long bytesSent = 0;
        while (rangeEnumSized.MoveNext())
        {
            Console.WriteLine($"{rangeEnumSized.Current.Path}");
            bytesSent += 31;
            bytesSent = rangeEnumSized.Current.SubTree.Aggregate(bytesSent, (current, xx) => current + 33);
            // Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} AcValue:{rangeEnum.Current.Value.ToHexString()} ExValue:{leafsForSync[rangeEnum.Current.Key].ToHexString()}");
            // Assert.That(rangeEnum.Current.Value.SequenceEqual(leafsForSync[rangeEnum.Current.Key]), Is.True);
        }
        Console.WriteLine($"{bytesSent}");
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void GetSyncRangeForBigVerkleTreeAndHealTree(DbMode dbMode)
    {
        const int pathPoolCount = 100_00;
        const int numBlocks1 = 150;
        const int numBlocks2 = 10;
        const int leafPerBlock = 2;
        const int initialLeafCount = 10000;
        const int blockJump = 5;
        const int healingLoop = 5;
        const int numPathInOneHealingLoop = pathPoolCount / (healingLoop * blockJump);

        IVerkleTrieStore remoteStore = TestItem.GetVerkleStore(dbMode);
        VerkleTree remoteTree = new(remoteStore, LimboLogs.Instance);

        IVerkleTrieStore localStore = TestItem.GetVerkleStore(DbMode.MemDb);
        VerkleTree localTree = new(localStore, LimboLogs.Instance);

        Hash256[] pathPool = GetPedersenPathPool(pathPoolCount);
        SortedDictionary<Hash256, byte[]> leafs = new();


        for (int leafIndex = 0; leafIndex < initialLeafCount; leafIndex++)
        {
            byte[] value = new byte[32];
            Random.NextBytes(value);
            Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];
            remoteTree.Insert(path, value);
            leafs[path] = value;
        }
        remoteTree.Commit();
        remoteTree.CommitTree(0);


        for (int blockNumber = 1; blockNumber <= numBlocks1; blockNumber++)
        {
            for (int accountIndex = 0; accountIndex < leafPerBlock; accountIndex++)
            {
                byte[] leafValue = new byte[32];

                Random.NextBytes(leafValue);
                Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];

                if (leafs.ContainsKey(path))
                {
                    if (!(Random.NextSingle() > 0.5)) continue;
                    remoteTree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                }
                else
                {
                    remoteTree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                }
            }

            remoteTree.Commit();
            remoteTree.CommitTree(blockNumber);
        }

        Banderwagon root = default;
        ExecutionWitness executionWitness = default;
        SortedSet<byte[]> update = new(Bytes.Comparer);

        int startingHashIndex = 0;
        int endHashIndex = 0;
        for (int blockNumber = numBlocks1 + 1; blockNumber <= numBlocks1 + blockJump; blockNumber++)
        {
            for (int i = 0; i < healingLoop; i++)
            {
                endHashIndex = startingHashIndex + numPathInOneHealingLoop - 1;
                Console.WriteLine($"{startingHashIndex} {endHashIndex}");
                PathWithSubTree[] range =
                    remoteTree._verkleStateStore
                        .GetLeafRangeIterator(
                            pathPool[startingHashIndex].Bytes.Slice(0,31).ToArray(),
                            pathPool[endHashIndex].Bytes.Slice(0,31).ToArray(),
                            remoteTree.StateRoot, 10000000)
                        .ToArray();
                ProcessSubTreeRange(remoteTree, localTree, blockNumber, remoteTree.StateRoot, range);

                startingHashIndex = endHashIndex + 1;
            }

            if (update.Count != 0)
            {
                // use execution witness to heal
                bool insertedWitness = localTree.InsertIntoStatelessTree(executionWitness, root, false);
                Assert.IsTrue(insertedWitness);
                localTree.CommitTree(0);
            }

            update = new SortedSet<byte[]>(Bytes.Comparer);
            for (int accountIndex = 0; accountIndex < leafPerBlock; accountIndex++)
            {
                byte[] leafValue = new byte[32];
                Random.NextBytes(leafValue);
                Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];

                if (leafs.ContainsKey(path))
                {
                    if (!(Random.NextSingle() > 0.5)) continue;
                    remoteTree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    update.Add(path.Bytes.ToArray());
                }
                else
                {
                    remoteTree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    update.Add(path.Bytes.ToArray());
                }
            }

            remoteTree.Commit();
            remoteTree.CommitTree(blockNumber);

            executionWitness = remoteTree.GenerateExecutionWitness(update.ToArray(), out root);
        }

        endHashIndex = startingHashIndex + 1000;
        while (endHashIndex < pathPool.Length - 1)
        {
            endHashIndex = startingHashIndex + 1000;
            if (endHashIndex > pathPool.Length - 1)
            {
                endHashIndex = pathPool.Length - 1;
            }
            Console.WriteLine($"{startingHashIndex} {endHashIndex}");

            PathWithSubTree[] range = remoteTree._verkleStateStore.GetLeafRangeIterator(
                pathPool[startingHashIndex].Bytes.Slice(0,31).ToArray(),
                pathPool[endHashIndex].Bytes.Slice(0,31).ToArray(),
                remoteTree.StateRoot, 100000000).ToArray();
            ProcessSubTreeRange(remoteTree, localTree, numBlocks1 + numBlocks2, remoteTree.StateRoot, range);

            startingHashIndex += 1000;
        }



        if (update.Count != 0)
        {
            // use execution witness to heal
            bool insertedWitness = localTree.InsertIntoStatelessTree(executionWitness, root, false);
            Assert.IsTrue(insertedWitness);
            localTree.CommitTree(0);
        }

        VerkleTreeDumper oldTreeDumper = new();
        VerkleTreeDumper newTreeDumper = new();

        localTree.Accept(oldTreeDumper, localTree.StateRoot);
        remoteTree.Accept(newTreeDumper, remoteTree.StateRoot);

        Console.WriteLine("oldTreeDumper");
        Console.WriteLine(oldTreeDumper.ToString());
        Console.WriteLine("newTreeDumper");
        Console.WriteLine(newTreeDumper.ToString());

        oldTreeDumper.ToString().Should().BeEquivalentTo(newTreeDumper.ToString());

        Assert.IsTrue(oldTreeDumper.ToString().SequenceEqual(newTreeDumper.ToString()));
    }

    private static void ProcessSubTreeRange(VerkleTree remoteTree, VerkleTree localTree, int blockNumber, Hash256 stateRoot, PathWithSubTree[] subTrees)
    {
        Stem startingStem = subTrees[0].Path;
        Stem endStem = subTrees[^1].Path;
        // Stem limitHash = Stem.MaxValue;

        VerkleProof proof = remoteTree.CreateVerkleRangeProof(startingStem, endStem, out Banderwagon root);

        bool isTrue = localTree.CreateStatelessTreeFromRange(proof, root, startingStem, endStem, subTrees);
        Assert.IsTrue(isTrue);
    }


    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void CreateHugeTree(DbMode dbMode)
    {
        long block = 0;
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(dbMode);
        Dictionary<byte[], byte[]?> kvMap = new(Bytes.EqualityComparer);
        byte[] key = new byte[32];
        byte[] value = new byte[32];
        DateTime start = DateTime.Now;
        for (int i = 0; i < numKeys; i++)
        {
            Random.NextBytes(key);
            Random.NextBytes(value);
            kvMap[key.AsSpan().ToArray()] = value.AsSpan().ToArray();
            tree.Insert((Hash256)key, value);
        }
        DateTime check1 = DateTime.Now;
        tree.Commit();
        tree.CommitTree(block++);
        DateTime check2 = DateTime.Now;
        Console.WriteLine($"{block} Insert: {(check1 - start).TotalMilliseconds}");
        Console.WriteLine($"{block} Flush: {(check2 - check1).TotalMilliseconds}");

        SortedSet<byte[]> keys = new(Bytes.Comparer);
        for (int i = 10; i < numKeys; i += 10)
        {
            DateTime check5 = DateTime.Now;
            Random.NextBytes(key);
            Random.NextBytes(value);
            for (int j = (i-10); j < i; j += 1)
            {
                Random.NextBytes(key);
                Random.NextBytes(value);
                kvMap[key.AsSpan().ToArray()] = value.AsSpan().ToArray();
                tree.Insert((Hash256)key, value);
                keys.Add(key.AsSpan().ToArray());
            }
            DateTime check3 = DateTime.Now;
            tree.Commit();
            tree.CommitTree(block++);
            DateTime check4 = DateTime.Now;
            Console.WriteLine($"{block} Insert: {(check3 - check5).TotalMilliseconds}");
            Console.WriteLine($"{block} Flush: {(check4 - check3).TotalMilliseconds}");
        }
        DateTime check6 = DateTime.Now;
        Console.WriteLine($"Loop Time: {(check6 - check2).TotalMilliseconds}");
        Console.WriteLine($"Total Time: {(check6 - start).TotalMilliseconds}");


        byte[][] keysArray = keys.ToArray();
        using IEnumerator<KeyValuePair<byte[], byte[]>> rangeEnum =
            tree._verkleStateStore.GetLeafRangeIterator(keysArray[30], keysArray[90], 180).GetEnumerator();

        while (rangeEnum.MoveNext())
        {
            Console.WriteLine($"Key:{rangeEnum.Current.Key.ToHexString()} Value:{rangeEnum.Current.Value.ToHexString()}");
            Assert.That(rangeEnum.Current.Value.SequenceEqual(kvMap[rangeEnum.Current.Key]), Is.True);
        }
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestRangeIterator(DbMode dbMode)
    {
        const int pathPoolCount = 100_000;
        const int leafPerBlock = 10;

        IVerkleTrieStore store = TestItem.GetVerkleStore(dbMode);
        VerkleTree tree = new(store, LimboLogs.Instance);

        Hash256[] pathPool = GetPedersenPathPool(pathPoolCount);
        SortedDictionary<Hash256, byte[]> leafs = new();

        for (int leafIndex = 0; leafIndex < 10000; leafIndex++)
        {
            byte[] value = new byte[32];
            Random.NextBytes(value);
            Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];
            tree.Insert(path, value);
            leafs[path] = value;
            Console.WriteLine($"blockNumber:{0} nKey:{path} nValue:{value.ToHexString()}");
        }

        tree.Commit();
        tree.CommitTree(0);

        for (int blockNumber = 1; blockNumber <= 180; blockNumber++)
        {
            for (int accountIndex = 0; accountIndex < leafPerBlock; accountIndex++)
            {
                byte[] leafValue = new byte[32];

                Random.NextBytes(leafValue);
                Hash256 path = pathPool[Random.Next(pathPool.Length - 1)];

                if (leafs.ContainsKey(path))
                {
                    if (!(Random.NextSingle() > 0.5)) continue;
                    Console.WriteLine($"blockNumber:{blockNumber} uKey:{path} uValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                    Console.WriteLine("new values");
                }
                else
                {
                    Console.WriteLine($"blockNumber:{blockNumber} nKey:{path} nValue:{leafValue.ToHexString()}");
                    tree.Insert(path, leafValue);
                    leafs[path] = leafValue;
                }
            }

            tree.Commit();
            tree.CommitTree(blockNumber);
        }

        KeyValuePair<byte[], byte[]>[] rangeEnum =
            tree._verkleStateStore.GetLeafRangeIterator(Pedersen.Zero.Bytes.ToArray(), Pedersen.MaxValue.Bytes.ToArray(), 180)
                .ToArray();

        int index = 0;
        foreach (KeyValuePair<Hash256, byte[]> leaf in leafs)
        {
            Console.WriteLine($"{leaf.Key} {rangeEnum[index].Key.ToHexString()}");
            Console.WriteLine($"{leaf.Value.ToHexString()} {rangeEnum[index].Value.ToArray()}");
            Assert.IsTrue(leaf.Key.Bytes.SequenceEqual(rangeEnum[index].Key));
            Assert.IsTrue(leaf.Value.SequenceEqual(rangeEnum[index].Value));
            index++;
        }
    }
}
