// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TreeNodes;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TreeStore;

public class ReadOnlyVerkleStateStore : IReadOnlyVerkleTreeStore
{
    public static Span<byte> RootNodeKey => Array.Empty<byte>();
    private readonly VerkleMemoryDb _keyValueStore;
    private readonly IVerkleTreeStore _verkleStateStore;

    public ReadOnlyVerkleStateStore(IVerkleTreeStore verkleStateStore, VerkleMemoryDb keyValueStore)
    {
        _verkleStateStore = verkleStateStore;
        _keyValueStore = keyValueStore;
    }

    public Hash256 StateRoot
    {
        get
        {
            _keyValueStore.GetInternalNode(RootNodeKey, out InternalNode? value);
            return value is null ? _verkleStateStore.StateRoot : new Hash256(value.Bytes);
        }

        set
        {
            _keyValueStore.LeafTable.Clear();
            _keyValueStore.InternalTable.Clear();
            MoveToStateRoot(value);
        }
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        return _keyValueStore.GetLeaf(key, out var value)
            ? value
            : _verkleStateStore.GetLeaf(key, stateRoot);
    }

    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        return _keyValueStore.GetInternalNode(key, out InternalNode? value)
            ? value
            : _verkleStateStore.GetInternalNode(key, stateRoot);
    }

    public void InsertBatch(long blockNumber, VerkleMemoryDb batch, bool skipRoot)
    {
    }

    public bool HasStateForBlock(Hash256 stateRoot)
    {
        return _verkleStateStore.HasStateForBlock(stateRoot);
    }

    public bool MoveToStateRoot(Hash256 stateRoot)
    {
        _keyValueStore.LeafTable.Clear();
        _keyValueStore.InternalTable.Clear();
        return _verkleStateStore.MoveToStateRoot(stateRoot);
    }

    public IReadOnlyVerkleTreeStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(_verkleStateStore, keyValueStore);
    }

    public ulong GetBlockNumber(Hash256 rootHash)
    {
        return _verkleStateStore.GetBlockNumber(rootHash);
    }

    public void InsertRootNodeAfterSyncCompletion(byte[] rootHash, long blockNumber)
    {
        throw new NotImplementedException();
    }

    public void InsertSyncBatch(long blockNumber, VerkleMemoryDb batch)
    {
        throw new NotImplementedException();
    }

    public event EventHandler<InsertBatchCompletedV1>? InsertBatchCompletedV1
    {
        add { }
        remove { }
    }
    public event EventHandler<InsertBatchCompletedV2>? InsertBatchCompletedV2
    {
        add { }
        remove { }
    }
    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add { }
        remove { }
    }
    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange, byte[] toRange,
        Hash256 stateRoot)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, stateRoot);
    }

    public IEnumerable<PathWithSubTree> GetLeafRangeIterator(Stem fromRange, Stem toRange, Hash256 stateRoot,
        long bytes)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, stateRoot, bytes);
    }

    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
        _keyValueStore.SetLeaf(leafKey, leafValue);
    }

    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        _keyValueStore.SetInternalNode(internalNodeKey, internalNodeValue);
    }
}
