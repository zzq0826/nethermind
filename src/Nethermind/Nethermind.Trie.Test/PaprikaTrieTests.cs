// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie.Pruning;
using Nethermind.Trie.Test.Pruning;
using NUnit.Framework;

namespace Nethermind.Trie.Test;

public class PaprikaTrieTests
{
    private ILogManager _logManager = LimboLogs.Instance;

    [Test]
    public void Empty_tree()
    {
        MemDb memDb = new();
        using TrieStore trieStore = new(memDb, Prune.WhenCacheReaches(1.MB()), Persist.EveryBlock, _logManager);
        PatriciaTree patriciaTree = new(trieStore, _logManager);

        patriciaTree.Commit(blockNumber: 1);
        patriciaTree.UpdateRootHash();

        var rootHash = patriciaTree.RootHash;
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
        PatriciaTree patriciaTree = new(trieStore, _logManager);

        Account account = new(nonce, balance);

        patriciaTree.Set(key.Bytes, new AccountDecoder().Encode(account).Bytes);
        patriciaTree.Commit(0);
        patriciaTree.UpdateRootHash();

        Assert.That(patriciaTree.Root, Is.Not.Null);

        var leaf = patriciaTree.Root;
        Assert.That(leaf.IsLeaf, Is.True);
        Assert.That(leaf.Keccak, Is.EqualTo(new Keccak(Convert.FromHexString(hexString))));
    }

    private static class Values
    {
        public static readonly Keccak Key0 = new(new byte[]
            { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, });

        public static readonly Keccak Key1 = new(new byte[]
            { 1, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3, 4, 5, 6, 7, });

        public static readonly Keccak Key2 = new(new byte[]
            { 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6 });

        public static readonly UInt256 Balance0 = 13;
        public static readonly UInt256 Balance1 = 17;
        public static readonly UInt256 Balance2 = 19;

        public static readonly UInt256 Nonce0 = 23;
        public static readonly UInt256 Nonce1 = 29;
        public static readonly UInt256 Nonce2 = 31;
    }
}
