// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using Nethermind.Db;
using Nethermind.Trie.Store.Nodes;

namespace Nethermind.Trie.Store;

public interface ITreeDb<in T>: IDisposable where T: ITrieNode
{
    public ConcurrentDictionary<string, IDb> RegisteredNodeDbs { get; }

    public void GetNode<TN>(byte[] path, out TN? node) where TN : struct, T;
    public void SetNode<TN>(byte[] path, TN? node) where TN : struct, T;
    public void DeleteNode<TN>(byte[] path) where TN : struct, T;


    public void RegisterDb<TD>(string dbName, TD db) where TD : class, IDb
    {
        if (RegisteredNodeDbs.ContainsKey(dbName))
        {
            throw new ArgumentException($"{dbName} has already registered.");
        }

        RegisteredNodeDbs.TryAdd(dbName, db);
    }

    private TD GetDb<TD>(string dbName) where TD : class, IDb
    {
        if (!RegisteredNodeDbs.TryGetValue(dbName, out IDb? found))
        {
            throw new ArgumentException($"{dbName} database has not been registered in {nameof(DbProvider)}.");
        }

        if (!(found is TD result))
        {
            throw new IOException(
                $"An attempt was made to resolve DB {dbName} as {typeof(T)} while its type is {found.GetType()}.");
        }

        return result;
    }
}
