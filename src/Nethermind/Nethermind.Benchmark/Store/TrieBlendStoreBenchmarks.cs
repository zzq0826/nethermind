// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Dirichlet.Numerics;
using BenchmarkDotNet.Attributes;
using static Nethermind.Core.Test.Builders.TestItem;
using Nethermind.Trie.Pruning;
using Nethermind.Db;
using Nethermind.State;
using System.Security.Principal;
using BenchmarkDotNet.Diagnosers;
using System.Xml.Linq;
using Nethermind.Db.Rocks;
using Nethermind.Config;
using Nethermind.Db.Rocks.Config;
using Metrics = Nethermind.Trie.Metrics;
using System.IO;

namespace Nethermind.Benchmarks.Store;

[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[MediumRunJob]
public class TrieBlendStoreReadBenchmarks
{
    private const int pathPoolCount = 100_000;

    private Random _random = new(1876);
    private TrieStore _trieStore;
    private TrieBlendStore _trieBlendStore;
    private StateTree _stateTree;
    private BlendStateTree _blendStateTree;

    [Params(100, 1000, 10000)]
    public int NumberOfAccounts;

    private Keccak[] pathPool = new Keccak[pathPoolCount];
    private List<int> _pathsInUse = new List<int>();

    [GlobalSetup]
    public void Setup()
    {
        var stateDBSettings = new RocksDbSettings("testStateDB", "tmpStateDB") { DeleteOnStart = true };

        var stateDBSettings_2 = new RocksDbSettings("testStateDB_2", "tmpStateDB_2") { DeleteOnStart = true };
        var accountsDBSettings = new RocksDbSettings("testAccountsDB", "tmpAccountsDB") { DeleteOnStart = true };

        ConfigProvider configProvider = new();
        RocksDbFactory dbFactory = new RocksDbFactory(configProvider.GetConfig<IDbConfig>(), NullLogManager.Instance, @"C:\Temp");

        var stateDB = dbFactory.CreateDb(stateDBSettings);
        var stateDB_2 = dbFactory.CreateDb(stateDBSettings_2);
        var accountsDB = dbFactory.CreateDb(accountsDBSettings);

        _trieStore = new TrieStore(stateDB, NullLogManager.Instance);
        _stateTree = new StateTree(_trieStore, NullLogManager.Instance);

        _trieBlendStore = new TrieBlendStore(stateDB_2, accountsDB, No.Pruning, Persist.EveryBlock, NullLogManager.Instance);
        _blendStateTree = new BlendStateTree(_trieBlendStore, NullLogManager.Instance);

        for (int i = 0; i < pathPoolCount; i++)
        {
            byte[] key = new byte[32];
            ((UInt256)i).ToBigEndian(key);
            Keccak keccak = new Keccak(key);
            pathPool[i] = keccak;
        }

        // generate Remote Tree
        for (int accountIndex = 0; accountIndex < NumberOfAccounts; accountIndex++)
        {
            Account account = TestItem.GenerateRandomAccount(_random);
            int pathKey = _random.Next(pathPool.Length - 1);
            _pathsInUse.Add(pathKey);
            Keccak path = pathPool[pathKey];

            _stateTree.Set(path, account);
            _blendStateTree.Set(path, account);
        }
        _stateTree.Commit(100);
        _blendStateTree.Commit(100, trackPath: true);
    }


    [Benchmark]
    public void TrieStoreGetAccount()
    {
        StateTree localTree = new(_trieStore, NullLogManager.Instance)
        {
            RootHash = _stateTree.RootHash
        };
        for (int i = 0; i < NumberOfAccounts; i++)
        {
            //_stateTree.Get(pathPool[_pathsInUse[i]]);
            localTree.Get(pathPool[_pathsInUse[i]]);
        }
    }

    [Benchmark]
    public void TrieBlendStoreGetAccount()
    {
        for (int i = 0; i < NumberOfAccounts; i++)
        {
            _blendStateTree.GetDirect(pathPool[_pathsInUse[i]]);
        }
    }
}

public class TrieBlendStoreCommitBenchmarks
{
    private const int pathPoolCount = 100_000;

    private Random _random = new(1876);
    private TrieStore _trieStore;
    private TrieBlendStore _trieBlendStore;
    private StateTree _stateTree;
    private BlendStateTree _blendStateTree;

    [Params(100, 1000, 10000)]
    public int NumberOfAccounts;

    private Keccak[] pathPool = new Keccak[pathPoolCount];
    private SortedDictionary<Keccak, Account> accounts = new();

    [GlobalSetup]
    public void Setup()
    {
        var stateDBSettings = new RocksDbSettings("testStateDB", "tmpStateDB") { DeleteOnStart = true };

        var stateDBSettings_2 = new RocksDbSettings("testStateDB_2", "tmpStateDB_2") { DeleteOnStart = true };
        var accountsDBSettings = new RocksDbSettings("testAccountsDB", "tmpAccountsDB") { DeleteOnStart = true };

        ConfigProvider configProvider = new();
        RocksDbFactory dbFactory = new RocksDbFactory(configProvider.GetConfig<IDbConfig>(), NullLogManager.Instance, @"C:\Temp");

        var stateDB = dbFactory.CreateDb(stateDBSettings);
        var stateDB_2 = dbFactory.CreateDb(stateDBSettings_2);
        var accountsDB = dbFactory.CreateDb(accountsDBSettings);

        _trieStore = new TrieStore(stateDB, NullLogManager.Instance);
        _stateTree = new StateTree(_trieStore, NullLogManager.Instance);

        _trieBlendStore = new TrieBlendStore(stateDB_2, accountsDB, No.Pruning, Persist.EveryBlock, NullLogManager.Instance);
        _blendStateTree = new BlendStateTree(_trieBlendStore, NullLogManager.Instance);

        for (int i = 0; i < pathPoolCount; i++)
        {
            byte[] key = new byte[32];
            ((UInt256)i).ToBigEndian(key);
            Keccak keccak = new Keccak(key);
            pathPool[i] = keccak;
        }

        // generate Remote Tree
        for (int accountIndex = 0; accountIndex < NumberOfAccounts; accountIndex++)
        {
            Account account = TestItem.GenerateRandomAccount(_random);
            int pathKey = _random.Next(pathPool.Length - 1);
            Keccak path = pathPool[pathKey];
            accounts[path] = account;
        }
    }


    [Benchmark]
    public void TrieStoreCommit()
    {
        foreach (var accountInfo in accounts)
            _stateTree.Set(accountInfo.Key, accountInfo.Value);

        _stateTree.Commit(1);
    }

    [Benchmark]
    public void TrieBlendStoreCommit()
    {
        foreach (var accountInfo in accounts)
            _blendStateTree.Set(accountInfo.Key, accountInfo.Value);

        _blendStateTree.Commit(1, trackPath: true);
    }
}
