// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core.Extensions;
using Nethermind.Verkle.Tree.TreeNodes;

namespace Nethermind.Verkle.Tree.VerkleDb;

public class ReadOnlyVerkleMemoryDb
{
    public InternalStore InternalTable;
    public LeafStoreSorted LeafTable;
}

public class VerkleMemoryDb : IVerkleDb, IVerkleMemDb
{
    public VerkleMemoryDb()
    {
        LeafTable = new LeafStore(Bytes.SpanEqualityComparer);
        InternalTable = new InternalStore(Bytes.SpanEqualityComparer);
    }

    public VerkleMemoryDb(LeafStore leafTable, InternalStore internalTable)
    {
        LeafTable = leafTable;
        InternalTable = internalTable;
    }

    public bool GetLeaf(ReadOnlySpan<byte> key, out byte[]? value)
    {
        return LeafTable.TryGetValue(key, out value);
    }

    public bool GetInternalNode(ReadOnlySpan<byte> key, out InternalNode? value)
    {
        return InternalTable.TryGetValue(key, out value);
    }

    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
        LeafTable[leafKey] = leafValue;
    }

    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        InternalTable[internalNodeKey] = internalNodeValue;
    }

    public void RemoveLeaf(ReadOnlySpan<byte> leafKey)
    {
        LeafTable.Remove(leafKey.ToArray(), out _);
    }

    public void RemoveInternalNode(ReadOnlySpan<byte> internalNodeKey)
    {
        InternalTable.Remove(internalNodeKey.ToArray(), out _);
    }

    public void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf)
    {
        foreach ((var key, var value) in keyLeaf) SetLeaf(key, value);
    }

    public void BatchInternalNodeInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> internalNodeKey)
    {
        foreach ((var key, InternalNode? value) in internalNodeKey) SetInternalNode(key, value);
    }

    public LeafStore LeafTable { get; }
    public InternalStore InternalTable { get; }
}
