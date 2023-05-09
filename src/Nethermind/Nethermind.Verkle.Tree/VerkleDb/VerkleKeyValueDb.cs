// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Db;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Serializers;

namespace Nethermind.Verkle.Tree.VerkleDb;

public class VerkleKeyValueDb : IVerkleDb, IVerkleKeyValueDb
{
    private readonly IDbProvider _dbProvider;

    public IDb LeafDb => _dbProvider.LeafDb;
    public IDb InternalNodeDb => _dbProvider.InternalNodesDb;

    public VerkleKeyValueDb(IDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public byte[]? GetLeaf(byte[] key) => LeafDb.Get(key);

    public InternalNode? GetInternalNode(byte[] key)
    {
        byte[]? value = InternalNodeDb[key];
        return value is null ? null : InternalNodeSerializer.Instance.Decode(value);
    }

    public bool GetLeaf(byte[] key, out byte[]? value)
    {
        value = GetLeaf(key);
        return value is not null;
    }

    public bool GetInternalNode(byte[] key, out InternalNode? value)
    {
        value = GetInternalNode(key);
        return value is not null;
    }

    public void SetLeaf(byte[] leafKey, byte[] leafValue) => _setLeaf(leafKey, leafValue, LeafDb);
    public void SetInternalNode(byte[] internalNodeKey, InternalNode internalNodeValue) => _setInternalNode(internalNodeKey, internalNodeValue, InternalNodeDb);

    public void RemoveLeaf(byte[] leafKey)
    {
        LeafDb.Remove(leafKey);
    }

    public void RemoveInternalNode(byte[] internalNodeKey)
    {
        InternalNodeDb.Remove(internalNodeKey);
    }


    public void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf)
    {
        using IBatch batch = LeafDb.StartBatch();
        foreach ((byte[] key, byte[]? value) in keyLeaf)
        {
            _setLeaf(key, value, batch);
        }
    }

    public void BatchInternalNodeInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> internalNode)
    {
        using IBatch batch = InternalNodeDb.StartBatch();
        foreach ((byte[] key, InternalNode? value) in internalNode)
        {
            _setInternalNode(key, value, batch);
        }
    }

    public byte[]? this[byte[] key]
    {
        get
        {
            return key.Length switch
            {
                32 => LeafDb[key],
                _ => InternalNodeDb[key]
            };
        }
        set
        {
            switch (key.Length)
            {
                case 32:
                    LeafDb[key] = value;
                    break;
                default:
                    InternalNodeDb[key] = value;
                    break;
            }
        }
    }

    private static void _setLeaf(byte[] leafKey, byte[]? leafValue, IKeyValueStore db) => db[leafKey] = leafValue;
    private static void _setInternalNode(byte[] internalNodeKey, InternalNode? internalNodeValue, IKeyValueStore db)
    {
        if (internalNodeValue != null) db[internalNodeKey] = InternalNodeSerializer.Instance.Encode(internalNodeValue).Bytes;
    }
}
