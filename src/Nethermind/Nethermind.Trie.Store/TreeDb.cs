// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using FastEnumUtility;
using Nethermind.Db;
using Nethermind.Trie.Store.Nodes;

namespace Nethermind.Trie.Store;

public class MerkleTreeDb: ITreeDb<IMerkleNode>
{
    public ConcurrentDictionary<string, IDb> RegisteredNodeDbs { get; }
    public MerkleTreeDb()
    {
        RegisteredNodeDbs = new ConcurrentDictionary<string, IDb>(StringComparer.InvariantCultureIgnoreCase);
        RegisterDb(TreeNodeType.Extension.ToName()!, new MemDb());
        RegisterDb(TreeNodeType.Branch.ToName()!, new MemDb());
        RegisterDb(TreeNodeType.Leaf.ToName()!, new MemDb());
    }

    public void GetNode<TN>(byte[] path, out TN? node) where TN : struct, IMerkleNode
    {
        IDb db = GetDb<IDb>(new TN().NodeType.GetLabel()!);

        node = new TN
        {
            FullRlp = db[path]
        };
    }

    public void SetNode<TN>(byte[] path, TN? node) where TN : struct, IMerkleNode
    {
        IDb db = GetDb<IDb>(new TN().NodeType.ToName()!);

        if(node.HasValue) db[path] = node.Value.FullRlp;
        else db[path] = Array.Empty<byte>();
    }

    public void DeleteNode<TN>(byte[] path) where TN : struct, IMerkleNode
    {
        IDb db = GetDb<IDb>(new TN().NodeType.GetLabel()!);
        db.Remove(path);
    }

    public void RegisterDb<T>(string dbName, T db) where T : class, IDb
    {
        if (RegisteredNodeDbs.ContainsKey(dbName))
        {
            throw new ArgumentException($"{dbName} has already registered.");
        }

        RegisteredNodeDbs.TryAdd(dbName, db);
    }

    private T GetDb<T>(string dbName) where T : class, IDb
    {
        if (!RegisteredNodeDbs.TryGetValue(dbName, out IDb? found))
        {
            throw new ArgumentException($"{dbName} database has not been registered in {nameof(DbProvider)}.");
        }

        if (!(found is T result))
        {
            throw new IOException(
                $"An attempt was made to resolve DB {dbName} as {typeof(T)} while its type is {found.GetType()}.");
        }

        return result;
    }
    public void Dispose()
    {
        foreach (KeyValuePair<string, IDb> registeredDb in RegisteredNodeDbs)
        {
            registeredDb.Value?.Dispose();
        }
    }
}
