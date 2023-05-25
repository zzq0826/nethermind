// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Verkle.Tree.Nodes;

namespace Nethermind.Verkle.Tree.VerkleDb;

public class VerkleMemoryDb : IVerkleDb, IVerkleMemDb
{
    public LeafStore LeafTable { get; }
    public InternalStore InternalTable { get; }

    public VerkleMemoryDb()
    {
        LeafTable = new LeafStore(Bytes.EqualityComparer);
        InternalTable = new InternalStore(Bytes.EqualityComparer);
    }

    public VerkleMemoryDb(LeafStore leafTable, InternalStore internalTable)
    {
        LeafTable = leafTable;
        InternalTable = internalTable;
    }

    public bool GetLeaf(byte[] key, out byte[]? value) => LeafTable.TryGetValue(key, out value);
    public bool GetInternalNode(byte[] key, out InternalNode? value) => InternalTable.TryGetValue(key, out value);

    public void SetLeaf(byte[] leafKey, byte[] leafValue) => LeafTable[leafKey] = leafValue;
    public void SetInternalNode(byte[] internalNodeKey, InternalNode internalNodeValue) => InternalTable[internalNodeKey] = internalNodeValue;

    public void RemoveLeaf(byte[] leafKey) => LeafTable.Remove(leafKey, out _);
    public void RemoveInternalNode(byte[] internalNodeKey) => InternalTable.Remove(internalNodeKey, out _);

    public void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf)
    {
        foreach ((byte[] key, byte[]? value) in keyLeaf)
        {
            SetLeaf(key, value);
        }
    }

    public void BatchInternalNodeInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> internalNodeKey)
    {
        foreach ((byte[] key, InternalNode? value) in internalNodeKey)
        {
            SetInternalNode(key, value);
        }
    }


}
