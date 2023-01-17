// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Db;
using Nethermind.Verkle.VerkleDb;
using NUnit.Framework;

namespace Nethermind.Verkle.Test;

public class HistoryTests
{
    private readonly byte[] _array1To32 =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
    };
    private readonly byte[] _array1To32Last128 =
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 128,
    };
    private readonly byte[] _emptyArray =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    };
    private readonly byte[] _arrayAll1 =
    {
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1
    };

    private readonly byte[] _arrayAll0Last2 =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2
    };

    private readonly byte[] _arrayAll0Last3 =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3
    };

    private readonly byte[] _arrayAll0Last4 =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4
    };


    private readonly byte[] _keyVersion =
    {
        121, 85, 7, 198, 131, 230, 143, 90, 165, 129, 173, 81, 186, 89, 19, 191, 13, 107, 197, 120, 243, 229, 224, 183, 72, 25, 6, 8, 210, 159, 31, 0,
    };
    private readonly byte[] _keyBalance =
    {
        121, 85, 7, 198, 131, 230, 143, 90, 165, 129, 173, 81, 186, 89, 19, 191, 13, 107, 197, 120, 243, 229, 224, 183, 72, 25, 6, 8, 210, 159, 31, 1,
    };
    private readonly byte[] _keyNonce =
    {
        121, 85, 7, 198, 131, 230, 143, 90, 165, 129, 173, 81, 186, 89, 19, 191, 13, 107, 197, 120, 243, 229, 224, 183, 72, 25, 6, 8, 210, 159, 31, 2,
    };
    private readonly byte[] _keyCodeCommitment = {
        121, 85, 7, 198, 131, 230, 143, 90, 165, 129, 173, 81, 186, 89, 19, 191, 13, 107, 197, 120, 243, 229, 224, 183, 72, 25, 6, 8, 210, 159, 31, 3,
    };
    private readonly byte[] _keyCodeSize =
    {
        121, 85, 7, 198, 131, 230, 143, 90, 165, 129, 173, 81, 186, 89, 19, 191, 13, 107, 197, 120, 243, 229, 224, 183, 72, 25, 6, 8, 210, 159, 31, 4,
    };
    private readonly byte[] _valueEmptyCodeHashValue =
    {
        197, 210, 70, 1, 134, 247, 35, 60, 146, 126, 125, 178, 220, 199, 3, 192, 229, 0, 182, 83, 202, 130, 39, 59, 123, 250, 216, 4, 93, 133, 164, 112,
    };

    private static string GetDbPathForTest()
    {
        string tempDir = Path.GetTempPath();
        string dbname = "VerkleTrie_TestID_" + TestContext.CurrentContext.Test.ID;
        return Path.Combine(tempDir, dbname);
    }

    private static VerkleTree GetVerkleTreeForTest(DbMode dbMode)
    {
        IDbProvider provider;
        switch (dbMode)
        {
            case DbMode.MemDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, null);
                return new VerkleTree(provider);
            case DbMode.PersistantDb:
                provider = VerkleDbFactory.InitDatabase(dbMode, GetDbPathForTest());
                return new VerkleTree(provider);
            case DbMode.ReadOnlyDb:
            default:
                throw new ArgumentOutOfRangeException(nameof(dbMode), dbMode, null);
        }
    }

    private VerkleTree GetFilledVerkleTreeForTest(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);

        tree.Insert(_keyVersion, _emptyArray);
        tree.Insert(_keyBalance, _emptyArray);
        tree.Insert(_keyNonce, _emptyArray);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _emptyArray);
        tree.Flush(0);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);

        tree.Insert(_keyVersion, _arrayAll0Last2);
        tree.Insert(_keyBalance, _arrayAll0Last2);
        tree.Insert(_keyNonce, _arrayAll0Last2);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last2);
        tree.Flush(1);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);

        tree.Insert(_keyVersion, _arrayAll0Last3);
        tree.Insert(_keyBalance, _arrayAll0Last3);
        tree.Insert(_keyNonce, _arrayAll0Last3);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last3);
        tree.Flush(2);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last3);

        tree.Insert(_keyVersion, _arrayAll0Last4);
        tree.Insert(_keyBalance, _arrayAll0Last4);
        tree.Insert(_keyNonce, _arrayAll0Last4);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last4);
        tree.Flush(3);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last4);

        return tree;
    }

    [TearDown]
    public void CleanTestData()
    {
        string dbPath = GetDbPathForTest();
        if (Directory.Exists(dbPath))
        {
            Directory.Delete(dbPath, true);
        }
    }

        [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGetMultiBlockReverseState(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);

        tree.Insert(_keyVersion, _emptyArray);
        tree.Insert(_keyBalance, _emptyArray);
        tree.Insert(_keyNonce, _emptyArray);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _emptyArray);
        tree.Flush(0);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);

        tree.Insert(_keyVersion, _arrayAll0Last2);
        tree.Insert(_keyBalance, _arrayAll0Last2);
        tree.Insert(_keyNonce, _arrayAll0Last2);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last2);
        tree.Flush(1);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);

        tree.Insert(_keyVersion, _arrayAll0Last3);
        tree.Insert(_keyBalance, _arrayAll0Last3);
        tree.Insert(_keyNonce, _arrayAll0Last3);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last3);
        tree.Flush(2);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last3);

        tree.ReverseState();

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);

        tree.ReverseState();

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGetBatchMultiBlockReverseState(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);

        tree.Insert(_keyVersion, _emptyArray);
        tree.Insert(_keyBalance, _emptyArray);
        tree.Insert(_keyNonce, _emptyArray);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _emptyArray);
        tree.Flush(0);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);

        tree.Insert(_keyVersion, _arrayAll0Last2);
        tree.Insert(_keyBalance, _arrayAll0Last2);
        tree.Insert(_keyNonce, _arrayAll0Last2);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last2);
        tree.Flush(1);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);

        tree.Insert(_keyVersion, _arrayAll0Last3);
        tree.Insert(_keyBalance, _arrayAll0Last3);
        tree.Insert(_keyNonce, _arrayAll0Last3);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _arrayAll0Last3);
        tree.Flush(2);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last3);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last3);

        VerkleMemoryDb memory = tree.GetReverseMergedDiff(2, 1);

        tree.ApplyDiffLayer(memory, 2,1);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);
    }


    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestReverseDiffThenForwardDiff(DbMode dbMode)
    {
        VerkleTree tree = GetFilledVerkleTreeForTest(dbMode);

        VerkleMemoryDb memory = tree.GetReverseMergedDiff(3,1);

        tree.ApplyDiffLayer(memory, 3, 1);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last2);

        VerkleMemoryDb forwardMemory = tree.GetForwardMergedDiff(1, 3);

        tree.ApplyDiffLayer(forwardMemory, 1, 3);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_arrayAll0Last4);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_arrayAll0Last4);

    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestReverseStateOneBlock(DbMode dbMode)
    {
        VerkleTree tree = GetFilledVerkleTreeForTest(dbMode);
        DateTime start = DateTime.Now;
        tree.ReverseState();
        DateTime end = DateTime.Now;
        Console.WriteLine($"ReverseState() 1 Block: {(end - start).TotalMilliseconds}");
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestForwardStateOneBlock(DbMode dbMode)
    {
        VerkleTree tree = GetFilledVerkleTreeForTest(dbMode);
        tree.ReverseState();
        VerkleMemoryDb forwardMemory = tree.GetForwardMergedDiff(2, 3);
        DateTime start = DateTime.Now;
        tree.ApplyDiffLayer(forwardMemory, 2, 3);
        DateTime end = DateTime.Now;
        Console.WriteLine($"ForwardState() 1 Block Insert: {(end - start).TotalMilliseconds}");
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestBatchReverseDiffs(DbMode dbMode)
    {
       // VerkleTree tree = GetHugeVerkleTreeForTest(dbMode);
       // for (int i = 2;i <= 1000;i++) {
       //     DateTime start = DateTime.Now;
       //     IVerkleDiffDb reverseDiff = tree.GetReverseMergedDiff(1, i);
       //     DateTime check1 = DateTime.Now;
       //     tree.ReverseState(reverseDiff, (i -1));
       //     DateTime check2 = DateTime.Now;
       //     Console.WriteLine($"Batch Reverse Diff Fetch(1, {i}): {(check1 - start).TotalMilliseconds}");
       //     Console.WriteLine($"Batch Reverse State(2, {i-1}): {(check2 - check1).TotalMilliseconds}");
       //}
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestBatchForwardDiffs(DbMode dbMode)
    {
       // VerkleTree tree = GetHugeVerkleTreeForTest(dbMode);
       // for (int i = 2;i <= 1000;i++) {
       //     DateTime start = DateTime.Now;
       //     IVerkleDiffDb forwardDiff = tree.GetForwardMergedDiff(1, i);
       //     DateTime check1 = DateTime.Now;
       //     tree.ForwardState(reverseDiff, (i -1));
       //     DateTime check2 = DateTime.Now;
       //     Console.WriteLine($"Batch Forward Diff Fetch(1, {i}): {(check1 - start).TotalMilliseconds}");
       //     Console.WriteLine($"Batch Forward State(2, {i-1}): {(check2 - check1).TotalMilliseconds}");
       //}
    }


}
