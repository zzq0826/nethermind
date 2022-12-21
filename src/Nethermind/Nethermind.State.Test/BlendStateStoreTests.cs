// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;
using Nethermind.Trie.TrieBlend;
using NUnit.Framework;

namespace Nethermind.Store.Test
{
    [TestFixture]
    public class BlendStateStoreTests
    {
        private readonly Account _account0 = Build.An.Account.WithBalance(0).TestObject;
        private readonly Account _account1 = Build.An.Account.WithBalance(1).TestObject;
        private readonly Account _account2 = Build.An.Account.WithBalance(2).TestObject;
        private readonly Account _account3 = Build.An.Account.WithBalance(3).TestObject;

        [SetUp]
        public void Setup()
        {
            Trie.Metrics.TreeNodeHashCalculations = 0;
            Trie.Metrics.TreeNodeRlpDecodings = 0;
            Trie.Metrics.TreeNodeRlpEncodings = 0;
        }

        [Test]
        public void Minimal_hashes_when_setting_on_empty()
        {
            MemDb db = new();
            MemDb db2 = new();
            IPersistenceStrategy persistenceStrategy = Persist.EveryBlock;

            var directTrieStore = new TrieBlendStore(db, db2, Prune.WhenCacheReaches(250.MB()), persistenceStrategy, LimboLogs.Instance);
            BlendStateTree directTree = new(directTrieStore, LimboLogs.Instance);
            directTree.Set(TestItem.AddressA, _account0);
            directTree.Set(TestItem.AddressB, _account1);
            directTree.Set(TestItem.AddressC, _account2);
            directTree.Commit(0);
            var account_at_a_flat = directTree.Get(TestItem.AddressA);
            var account_at_a_trie = directTree.Get(Keccak.Compute(TestItem.AddressA.Bytes));

            Assert.IsTrue(CompareAccounts(account_at_a_trie, account_at_a_flat));
        }

        [Test]
        public void Commit_block_change_different_accounts()
        {
            MemDb db = new();
            MemDb leafs_db = new();
            IPersistenceStrategy persistenceStrategy = Persist.EveryBlock;
            var directTrieStore = new TrieBlendStore(db, leafs_db, Prune.WhenCacheReaches(250.MB()), persistenceStrategy, LimboLogs.Instance);
            BlendStateTree directTree = new(directTrieStore, LimboLogs.Instance);
            directTree.Set(TestItem.AddressA, _account0);
            directTree.Commit(0);
            directTree.Set(TestItem.AddressB, _account1);
            directTree.Set(TestItem.AddressC, _account2);
            directTree.Commit(1);
            //var account_at_a_flat = directTree.Get(TestItem.AddressA);
            var account_at_a_flat = directTree.GetDirect(Keccak.Compute(TestItem.AddressA.Bytes));
            var account_at_a_trie = directTree.Get(Keccak.Compute(TestItem.AddressA.Bytes));

            Assert.IsTrue(CompareAccounts(account_at_a_trie, account_at_a_flat));
        }

        [Test]
        public void Commit_block_change_same_accounts()
        {
            MemDb db = new();
            MemDb leafs_db = new();

            IPersistenceStrategy persistenceStrategy = Persist.EveryBlock;
            var directTrieStore = new TrieBlendStore(db, leafs_db, Prune.WhenCacheReaches(250.MB()), persistenceStrategy, LimboLogs.Instance);
            BlendStateTree directTree = new(directTrieStore, LimboLogs.Instance);
            directTree.Set(TestItem.AddressA, _account0);
            directTree.Commit(0);
            directTree.Set(TestItem.AddressA, _account1);
            directTree.Commit(1);
            var account_at_a_flat = directTree.Get(TestItem.AddressA);
            var account_at_a_trie = directTree.Get(Keccak.Compute(TestItem.AddressA.Bytes));

            Assert.IsTrue(CompareAccounts(account_at_a_trie, account_at_a_flat));
        }

        [Test]
        public void Commit_block_change_same_accounts_2()
        {
            MemDb db = new();
            MemDb leafs_db = new();

            var directTrieStore = new TrieBlendStore(db, leafs_db, No.Pruning, Persist.EveryBlock, LimboLogs.Instance);
            BlendStateTree directTree = new(directTrieStore, LimboLogs.Instance);
            directTree.Set(TestItem.AddressA, _account0);
            directTree.Commit(0);
            directTree.Set(TestItem.AddressB, _account1);
            directTree.Commit(1);
            directTree.Set(TestItem.AddressA, _account2);
            directTree.Commit(2);
            directTree.Set(TestItem.AddressC, _account3);
            directTree.Commit(3);

            var account_at_a_flat = directTree.Get(TestItem.AddressA);
            var account_at_a_trie = directTree.Get(Keccak.Compute(TestItem.AddressA.Bytes));

            Assert.IsTrue(CompareAccounts(account_at_a_trie, account_at_a_flat));
        }

        [Test]
        public void Block_Layer_Persist_Log_And_Load()
        {
            MemDb db = new();
            AccountDecoder accountDecoder = new AccountDecoder();

            BlockDataLayer dataLayer = new BlockDataLayer(db, 0);
            dataLayer[TestItem.KeccakA.Bytes] = accountDecoder.Encode(_account0).Bytes;
            dataLayer[TestItem.KeccakB.Bytes] = accountDecoder.Encode(_account1).Bytes;
            dataLayer.PersistLog();

            BlockDataLayer restoredLayer = new BlockDataLayer(db);
            restoredLayer.LoadLog(0);

            Account acc0 = accountDecoder.Decode(dataLayer[TestItem.KeccakA.Bytes].AsRlpStream());
            Account acc1 = accountDecoder.Decode(dataLayer[TestItem.KeccakB.Bytes].AsRlpStream());

            Assert.AreEqual(1, db.WritesCount);
            Assert.AreEqual(1, db.ReadsCount);
            Assert.IsTrue(CompareAccounts(acc0, _account0));
            Assert.IsTrue(CompareAccounts(acc1, _account1));
        }

        [Test]
        public void Block_Layer_Persist_All_And_Load_With_Cached()
        {
            MemDb db = new();
            AccountDecoder accountDecoder = new AccountDecoder();

            BlockDataLayer dataLayer = new BlockDataLayer(db, 0);
            db[TestItem.KeccakC.Bytes] = accountDecoder.Encode(_account2).Bytes;

            dataLayer[TestItem.KeccakA.Bytes] = accountDecoder.Encode(_account0).Bytes;
            dataLayer[TestItem.KeccakB.Bytes] = accountDecoder.Encode(_account1).Bytes;
            //load element to read cache
            _ = dataLayer[TestItem.KeccakC.Bytes];
            dataLayer.Persist(0);

            BlockDataLayer restoredLayer = new BlockDataLayer(db);
            restoredLayer.LoadLog(0);

            Assert.IsTrue(restoredLayer.IsPersisted);
            Assert.AreEqual(2, restoredLayer.ElementCount);


            Account acc0 = accountDecoder.Decode(dataLayer[TestItem.KeccakA.Bytes].AsRlpStream());
            Account acc1 = accountDecoder.Decode(dataLayer[TestItem.KeccakB.Bytes].AsRlpStream());

            Assert.IsTrue(CompareAccounts(acc0, _account0));
            Assert.IsTrue(CompareAccounts(acc1, _account1));
        }

        [Test]
        public void Save_node_directly_and_read_account()
        {
            MemDb db = new();
            MemDb leafs_db = new();
            AccountDecoder accountDecoder = new AccountDecoder();

            var directTrieStore = new TrieBlendStore(db, leafs_db, No.Pruning, Persist.EveryBlock, LimboLogs.Instance);
            BlendStateTree directTree = new(directTrieStore, LimboLogs.Instance);

            MemDb db_remote = new();
            TrieStore remoteStore = new TrieStore(db_remote, LimboLogs.Instance);
            StateTree remoteTree = new StateTree(remoteStore, LimboLogs.Instance);

            TrieNode node = TrieNodeFactory.CreateLeaf(HexPrefix.Leaf(TestItem.AddressA.Bytes), accountDecoder.Encode(_account0).Bytes);
            node.ResolveKey(remoteStore, false);

            directTrieStore.SaveNode(1, Keccak.Compute(TestItem.AddressA.Bytes).Bytes, node);

            Account account0 = directTree.Get(TestItem.AddressA);

            Assert.IsTrue(CompareAccounts(account0, _account0));
        }

        [Test]
        public void Large_random_tree_test()
        {
            int pathPoolCount = 100_000;
            int accountCount = 10000;

            Random _random = new(1876);
            TrieBlendStore _trieBlendStore;
            BlendStateTree _blendStateTree;
            Keccak[] pathPool = new Keccak[pathPoolCount];
            List<int> _pathsInUse = new List<int>();

            _trieBlendStore = new TrieBlendStore(new MemDb(), new MemDb(), No.Pruning, Persist.EveryBlock, NullLogManager.Instance);
            _blendStateTree = new BlendStateTree(_trieBlendStore, NullLogManager.Instance);

            for (int i = 0; i < pathPoolCount; i++)
            {
                byte[] key = new byte[32];
                ((UInt256)i).ToBigEndian(key);
                Keccak keccak = new Keccak(key);
                pathPool[i] = keccak;
            }

            // generate Remote Tree
            for (int accountIndex = 0; accountIndex < accountCount; accountIndex++)
            {
                Account account = TestItem.GenerateRandomAccount(_random);
                int pathKey = _random.Next(pathPool.Length - 1);
                _pathsInUse.Add(pathKey);
                Keccak path = pathPool[pathKey];

                _blendStateTree.Set(path, account);
            }

            _blendStateTree.Commit(1);
        }

        private bool CompareAccounts(Account account1, Account account2)
        {
            return account1?.Balance == account2?.Balance &&
                    account1?.Nonce == account2?.Nonce &&
                    account1?.CodeHash == account2?.CodeHash &&
                    account1?.StorageRoot == account2?.StorageRoot;
        }
    }
}
