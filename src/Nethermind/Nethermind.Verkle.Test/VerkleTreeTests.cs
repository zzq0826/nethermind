using FluentAssertions;
using Nethermind.Db;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree;
using NUnit.Framework;

namespace Nethermind.Verkle.Test;


[TestFixture, Parallelizable(ParallelScope.All)]
public class VerkleTreeTests
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
        string dbname = $"VerkleTrie_TestID_{TestContext.CurrentContext.Test.ID}";
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
    public void TestInsertKey0Value0(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] key = _emptyArray;

        tree.Insert(key, key);
        AssertRootNode(tree.RootHash,
            "6B630905CE275E39F223E175242DF2C1E8395E6F46EC71DCE5557012C1334A5C");

        tree.Get(key).Should().BeEquivalentTo(key);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertKey1Value1(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] key = _array1To32;

        tree.Insert(key, key);
        AssertRootNode(tree.RootHash,
            "6F5E7CFC3A158A64E5718B0D2F18F564171342380F5808F3D2A82F7E7F3C2778");

        tree.Get(key).Should().BeEquivalentTo(key);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertSameStemTwoLeaves(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] keyA = _array1To32;

        byte[] keyB = _array1To32Last128;

        tree.Insert(keyA, keyA);
        AssertRootNode(tree.RootHash,
            "6F5E7CFC3A158A64E5718B0D2F18F564171342380F5808F3D2A82F7E7F3C2778");
        tree.Insert(keyB, keyB);
        AssertRootNode(tree.RootHash,
            "14EE5E5C5B698E363055B41DD3334F8168C7FCA4F85C5E30AB39CF9CC2FEEF70");

        tree.Get(keyA).Should().BeEquivalentTo(keyA);
        tree.Get(keyB).Should().BeEquivalentTo(keyB);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertKey1Val1Key2Val2(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] keyA = _emptyArray;
        byte[] keyB = _arrayAll1;

        tree.Insert(keyA, keyA);
        AssertRootNode(tree.RootHash,
            "6B630905CE275E39F223E175242DF2C1E8395E6F46EC71DCE5557012C1334A5C");
        tree.Insert(keyB, keyB);
        AssertRootNode(tree.RootHash,
            "5E208CDBA664A7B8FBDC26A1C1185F153A5F721CBA389625C18157CEF7D4931C");

        tree.Get(keyA).Should().BeEquivalentTo(keyA);
        tree.Get(keyB).Should().BeEquivalentTo(keyB);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertLongestPath(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] keyA = _emptyArray;
        byte[] keyB = (byte[])_emptyArray.Clone();
        keyB[30] = 1;

        tree.Insert(keyA, keyA);
        AssertRootNode(tree.RootHash,
            "6B630905CE275E39F223E175242DF2C1E8395E6F46EC71DCE5557012C1334A5C");
        tree.Insert(keyB, keyB);
        AssertRootNode(tree.RootHash,
            "3258D722AEA34B5AE7CB24A9B0175EDF0533C651FA09592E823B5969C729FB88");

        tree.Get(keyA).Should().BeEquivalentTo(keyA);
        tree.Get(keyB).Should().BeEquivalentTo(keyB);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertAndTraverseLongestPath(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] keyA = _emptyArray;
        tree.Insert(keyA, keyA);
        AssertRootNode(tree.RootHash,
            "6B630905CE275E39F223E175242DF2C1E8395E6F46EC71DCE5557012C1334A5C");

        byte[] keyB = (byte[])_emptyArray.Clone();
        keyB[30] = 1;
        tree.Insert(keyB, keyB);
        AssertRootNode(tree.RootHash,
            "3258D722AEA34B5AE7CB24A9B0175EDF0533C651FA09592E823B5969C729FB88");

        byte[] keyC = (byte[])_emptyArray.Clone();
        keyC[29] = 1;
        tree.Insert(keyC, keyC);
        AssertRootNode(tree.RootHash,
            "5B82B26A1A7E00A1E997ABD51FE3075D05F54AA4CB1B3A70607E62064FADAA82");

        tree.Get(keyA).Should().BeEquivalentTo(keyA);
        tree.Get(keyB).Should().BeEquivalentTo(keyB);
        tree.Get(keyC).Should().BeEquivalentTo(keyC);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestEmptyTrie(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        tree.RootHash.Should().BeEquivalentTo(FrE.Zero.ToBytes().ToArray());
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestSimpleUpdate(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);
        byte[] key = _array1To32;
        byte[] value = _emptyArray;
        tree.Insert(key, value);
        AssertRootNode(tree.RootHash,
            "140A25B322EAA1ADACD0EE1BB135ECA7B78FCF02B4B19E4A55B26B7A434F42AC");
        tree.Get(key).Should().BeEquivalentTo(value);

        tree.Insert(key, key);
        AssertRootNode(tree.RootHash,
            "6F5E7CFC3A158A64E5718B0D2F18F564171342380F5808F3D2A82F7E7F3C2778");
        tree.Get(key).Should().BeEquivalentTo(key);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGet(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);

        tree.Insert(_keyVersion, _emptyArray);
        AssertRootNode(tree.RootHash,
            "476C50753A22B270DA9D409C0F9AB655AB2506CE4EF831481DD455F0EA730FEF");

        tree.Insert(_keyBalance, _arrayAll0Last2);
        AssertRootNode(tree.RootHash,
            "6D9E4F2D418DE2822CE9C2F4193C0E155E4CAC6CF4170E44098DC49D4B571B7B");

        tree.Insert(_keyNonce, _emptyArray);
        AssertRootNode(tree.RootHash,
            "5C7AE53FE2AAE9852127140C1E2F5122BB3759A7975C0E7A1AEC7CAF7C711FDE");

        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        AssertRootNode(tree.RootHash,
            "3FD5FA25042DB0304792BFC007514DA5B777516FFBDAA5658AF36D355ABD9BD8");

        tree.Insert(_keyCodeSize, _emptyArray);
        AssertRootNode(tree.RootHash,
            "006BD679A8204502DCBF9A002F0B828AECF5A29A3A5CE454E651E3A96CC02FE2");

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_arrayAll0Last2);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestValueSameBeforeAndAfterFlush(DbMode dbMode)
    {
        VerkleTree tree = GetVerkleTreeForTest(dbMode);


        tree.Insert(_keyVersion, _emptyArray);
        tree.Insert(_keyBalance, _emptyArray);
        tree.Insert(_keyNonce, _emptyArray);
        tree.Insert(_keyCodeCommitment, _valueEmptyCodeHashValue);
        tree.Insert(_keyCodeSize, _emptyArray);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);

        tree.Flush(0);

        tree.Get(_keyVersion).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyBalance).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyNonce).Should().BeEquivalentTo(_emptyArray);
        tree.Get(_keyCodeCommitment).Should().BeEquivalentTo(_valueEmptyCodeHashValue);
        tree.Get(_keyCodeSize).Should().BeEquivalentTo(_emptyArray);
    }

    [TestCase(DbMode.MemDb)]
    [TestCase(DbMode.PersistantDb)]
    public void TestInsertGetMultiBlock(DbMode dbMode)
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
    }

    private static void AssertRootNode(byte[] realRootHash, string expectedRootHash)
    {
        Convert.ToHexString(realRootHash).Should()
            .BeEquivalentTo(expectedRootHash);
    }
}
