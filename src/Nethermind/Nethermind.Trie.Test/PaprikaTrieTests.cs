// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Security.Cryptography.Xml;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.Trie.Test.Pruning;
using NUnit.Framework;

namespace Nethermind.Trie.Test;

/// <summary>
///
/// </summary>
public class PaprikaTrieTests
{
    private static readonly ILogManager _logManager = LimboLogs.Instance;

    [Test]
    public void Empty_tree()
    {
        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, Prune.WhenCacheReaches(1.MB()), Persist.EveryBlock, _logManager);
        PatriciaTree patriciaTree = new(trieStore, _logManager);

        patriciaTree.Commit(blockNumber: 1);
        patriciaTree.UpdateRootHash();

        Keccak? rootHash = patriciaTree.RootHash;
        Assert.That(rootHash, Is.EqualTo(Keccak.EmptyTreeHash));
    }

    private static object[] _keysBalancesNoncesHexStrings =
    {
        new object[]
        {
            Values.Key0, Values.Balance0, Values.Nonce0,
            "E2533A0A0C4F1DDB72FEB7BFAAD12A83853447DEAAB6F28FA5C443DD2D37C3FB",
        },
        new object[]
        {
            Values.Key1, Values.Balance1, Values.Nonce1,
            "DD358AF6B1D8E875FBA0E585710D054F14DD9D06FA3C8C5F2BAF66F413178F82",
        },
        new object[]
        {
            Values.Key2, Values.Balance2, Values.Nonce2,
            "A654F039A5F9E9F30C89F21555C92F1CB1E739AF11A9E9B12693DEDC6E76F628",
        },
        new object[]
        {
            Values.Key0, UInt256.MaxValue, Values.Nonce0,
            "FC790A3674B6847C609BB91B7014D67B2E70FF0E60F53AC49A8A45F1FECF35A6",
        },
        new object[]
        {
            Values.Key0, Values.Balance0, UInt256.MaxValue,
            "F5EC898875FFCF5F161718F09696ABEDA7B77DFB8F8CEBAFA57129F77C8D720B",
        },
    };

    [Test]
    [TestCaseSource(nameof(_keysBalancesNoncesHexStrings))]
    public void Leaf_keccak(Keccak key, UInt256 balance, UInt256 nonce, string hexString)
    {
        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree patriciaTree = new(trieStore, _logManager);

        Account account = new(nonce, balance);

        patriciaTree.Set(key, account);
        patriciaTree.Commit(0);
        patriciaTree.UpdateRootHash();

        Assert.That(patriciaTree.Root, Is.Not.Null);

        var leaf = patriciaTree.Root;
        Assert.That(leaf.IsLeaf, Is.True);
        Assert.That(leaf.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [Test]
    public void Branch_two_leafs()
    {
        const int shift = 4;
        const byte nibbleA = 0x10;
        UInt256 balanceA = Values.Balance0;
        UInt256 nonceA = Values.Nonce0;

        const byte nibbleB = 0x20;
        UInt256 balanceB = Values.Balance1;
        UInt256 nonceB = Values.Nonce1;

        Span<byte> span = stackalloc byte[32];
        span.Fill(0);

        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);

        span[0] = nibbleA;
        state.Set(new Keccak(span), new Account(nonceA, balanceA));

        span[0] = nibbleB;
        state.Set(new Keccak(span), new Account(nonceB, balanceB));

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root, Is.Not.Null);

        var root = state.Root;
        Assert.That(root.IsBranch, Is.True);

        Assert.That(root.GetChild(trieStore, nibbleA >> shift)!.IsLeaf, Is.True);
        Assert.That(root.GetChild(trieStore, nibbleB >> shift)!.IsLeaf, Is.True);

        const string hexString = "73130daa1ae507554a72811c06e28d4fee671bfe2e1d0cef828a7fade54384f9";
        Assert.That(root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [Test]
    public void Storage_branch_with_short_leaves()
    {
        byte[] value = new UInt256(1).ToBigEndian().WithoutLeadingZeros().ToArray();

        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StorageTree state = new(trieStore, _logManager);

        ValueKeccak key0 = new();
        key0.BytesAsSpan[31] = 0x0A;

        ValueKeccak key1 = new();
        key1.BytesAsSpan[31] = 0x0B;

        state.Set(key0, value);
        state.Set(key1, value);

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root, Is.Not.Null);
    }

    [TestCase(0, "7f7fd47a28dc4dbfd1b1b33d254da8be74deab55bef81a02c232ca9957e05689", TestName = "From the Root")]
    [TestCase(Keccak.Size - 16, "6cb4831677e5dc9f8b6aaa9554c8be152ead051c35bdadbc19ae7a0242904836",
        TestName = "Split in the middle")]
    public void Skewed_tree(int startFromByte, string hexString)
    {
        using MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);
        Random random = new(17);

        // "0000"
        // "1000"
        // "1100";
        // "1110";
        // "1111";
        // "2000"
        // "2100"
        // "2110"
        // "2111"
        // "2200"
        // ...

        int count = 0;

        Span<byte> key = stackalloc byte[32];
        for (int nibble0 = 0; nibble0 < 16; nibble0++)
        {
            for (int nibble1 = 0; nibble1 <= nibble0; nibble1++)
            {
                for (int nibble2 = 0; nibble2 <= nibble1; nibble2++)
                {
                    for (int nibble3 = 0; nibble3 <= nibble2; nibble3++)
                    {
                        key.Clear();
                        random.NextBytes(key.Slice(startFromByte));
                        byte b0 = (byte)((nibble0 << 4) | nibble1);
                        key[startFromByte] = b0;

                        byte b1 = (byte)((nibble2 << 4) | nibble3);
                        key[startFromByte + 1] = b1;

                        state.Set(new ValueKeccak(key), new Account(b0, b1, Keccak.EmptyTreeHash, new Keccak(key)));
                        count++;
                    }
                }
            }
        }

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [TestCase(Keccak.Size - 1, "456af6194513cb6b8fd475087fdac6ab60bb3b3f154d06e19e3b18cd8c7e7092",
        TestName = "At the bottom")]
    public void Skewed_tree_short(int startFromByte, string hexString)
    {
        using MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);
        Random random = new(17);

        const int maxNibble = 3;
        // "00"
        // "10"
        // "11"
        // "20"
        // "21"
        // "22"
        // ...

        Span<byte> key = stackalloc byte[32];

        for (int nibble0 = 0; nibble0 < maxNibble; nibble0++)
        {
            for (int nibble1 = 0; nibble1 <= nibble0; nibble1++)
            {
                key.Clear();
                random.NextBytes(key.Slice(startFromByte));
                byte b0 = (byte)((nibble0 << 4) | nibble1);
                key[startFromByte] = b0;

                state.Set(new ValueKeccak(key), new Account(b0, (uint)b0 + 1, Keccak.EmptyTreeHash, new Keccak(key)));
            }
        }

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [TestCase(16, "80ad0d5d5f4912fe515d70f62b0c5359ac8fb26dbaf52a78fafb7a820252438c")]
    [TestCase(128, "45f09d49b6b1ca5f2b0cb82bc7fb3b381ff2ce95bd69a5eda8ce13b48855c2e4")]
    [TestCase(512, "b71064764d77f2122778e3892b37470854efd4a4acd8a1955bbe7dcfa0bc161c")]
    public void Scattered_tree(int size, string rootHash)
    {
        using MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);
        Random random = new(17);
        Span<byte> key = stackalloc byte[32];

        for (uint i = 0; i < size; i++)
        {
            key.Clear();

            int at = random.Next(64);
            int nibble = random.Next(16);

            if (at % 2 == 0)
            {
                nibble <<= 4;
            }

            key[at / 2] = (byte)nibble;

            state.Set(new ValueKeccak(key), new Account(i, i, Keccak.EmptyTreeHash, new Keccak(key)));
        }

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(rootHash))));
    }

    [Test]
    public void Extension()
    {
        UInt256 balanceA = Values.Balance0;
        UInt256 nonceA = Values.Nonce0;

        UInt256 balanceB = Values.Balance1;
        UInt256 nonceB = Values.Nonce1;

        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);

        state.Set(new Keccak(Values.Key0), new Account(nonceA, balanceA));
        state.Set(new Keccak(Values.Key1), new Account(nonceB, balanceB));

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root, Is.Not.Null);

        var root = state.Root;
        Assert.That(root.IsExtension, Is.True);

        const string hexString = "a624947d9693a5cba0701897b3a48cb9954c2f4fd54de36151800eb2c7f6bf50";
        Assert.That(root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [TestCase(1000, "b255eb6261dc19f0639d13624e384b265759d2e4171c0eb9487e82d2897729f0")]
    [TestCase(10_000, "48864c880bd7610f9bad9aff765844db83c17cab764f5444b43c0076f6cf6c03")]
    [TestCase(1_000_000, "e46e17a7ffa62ba32679893e6ccb4d9e48a9b044a88f22ff02004e6cc7f005b8")]
    [TestCase(10_000_000, "a52d8ca37ed3310fa024563ad432df953fabb2130523f78adb1830bda9beccbe")]
    public void Big_random(int count, string hexString)
    {
        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, LimboLogs.Instance);
        StateTree state = new(trieStore, _logManager);

        Random random = new(13);
        Span<byte> key = stackalloc byte[32];

        for (int i = 0; i < count; i++)
        {
            random.NextBytes(key);
            uint value = (uint)random.Next();
            state.Set(new Keccak(key), new Account(value, value));
        }

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root, Is.Not.Null);

        TrieNode root = state.Root;

        Assert.That(root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    [TestCase(1, 1, "954f21233681f1b941ef67b30c85b64bfb009452b7f01b28de28eb4c1d2ca258")]
    [TestCase(1, 100, "c8cf5e6b84e39beeac713a42546cc977581d9b31307efa2b1b288ccd828f278e")]
    [TestCase(100, 1, "68965a86aec45d3863d2c6de07fcdf75ac420dca0c0f45776704bfc9295593ac")]
    [TestCase(1000, 1, "b8bdf00f1f389a1445867e5c14ccf17fd21d915c01492bed3e70f74de7f42248")]
    [TestCase(1000, 1000, "4f474648522dc59d4d4a918e301d9d36ac200029027d28605cd2ab32f37321f8")]
    public void Big_random_storage(int count, int storageCount, string hexString)
    {
        ILogManager logs = NullLogManager.Instance;

        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, new TestPruningStrategy(true), Persist.EveryBlock, logs);
        StateTree state = new(trieStore, _logManager);

        Random random = new(13);

        for (int i = 0; i < count; i++)
        {
            // account data first
            ValueKeccak keccak = default;
            random.NextBytes(keccak.BytesAsSpan);

            uint value = (uint)random.Next();

            // storage data second
            StorageTree storage = new(trieStore, logs);

            for (int j = 0; j < storageCount; j++)
            {
                ValueKeccak storageKey = default;
                random.NextBytes(storageKey.BytesAsSpan);
                int storageValue = random.Next();

                storage.Set(storageKey, storageValue.ToByteArray());
            }

            storage.Commit(0);
            storage.UpdateRootHash();

            state.Set(keccak, new Account(value, value, storage.RootHash, Keccak.OfAnEmptyString));
        }

        state.Commit(0);
        state.UpdateRootHash();

        Assert.That(state.Root, Is.Not.Null);

        TrieNode root = state.Root;

        Assert.That(root.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    private static class Values
    {
        public static readonly Keccak Key0 = new(new byte[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7,
        });

        public static readonly Keccak Key1 = new(new byte[]
        {
            1, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7,
        });

        public static readonly Keccak Key2 = new(new byte[]
        {
            6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6
        });

        public static readonly UInt256 Balance0 = 13;
        public static readonly UInt256 Balance1 = 17;
        public static readonly UInt256 Balance2 = 19;

        public static readonly UInt256 Nonce0 = 23;
        public static readonly UInt256 Nonce1 = 29;
        public static readonly UInt256 Nonce2 = 31;
    }
}
