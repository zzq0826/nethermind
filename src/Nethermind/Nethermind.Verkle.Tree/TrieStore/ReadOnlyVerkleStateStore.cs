// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public class ReadOnlyVerkleStateStore : IVerkleTrieStore, ISyncTrieStore
{
    private VerkleStateStore _verkleStateStore;
    private VerkleMemoryDb _keyValueStore;

    public ReadOnlyVerkleStateStore(VerkleStateStore verkleStateStore, VerkleMemoryDb keyValueStore)
    {
        _verkleStateStore = verkleStateStore;
        _keyValueStore = keyValueStore;
    }

    public Hash256 StateRoot
    {
        get
        {
            _keyValueStore.GetInternalNode(VerkleStateStore.RootNodeKey, out InternalNode? value);
            return value is null ? _verkleStateStore.StateRoot : new Hash256(value.Bytes);
        }
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        return _keyValueStore.GetLeaf(key, out byte[]? value)
            ? value
            : _verkleStateStore.GetLeaf(key, stateRoot);
    }
    public InternalNode? GetInternalNode(ReadOnlySpan<byte> key, Hash256? stateRoot = null)
    {
        return _keyValueStore.GetInternalNode(key, out InternalNode? value)
            ? value
            : _verkleStateStore.GetInternalNode(key, stateRoot);
    }
    public void SetLeaf(ReadOnlySpan<byte> leafKey, byte[] leafValue)
    {
        _keyValueStore.SetLeaf(leafKey, leafValue);
    }
    public void SetInternalNode(ReadOnlySpan<byte> internalNodeKey, InternalNode internalNodeValue)
    {
        _keyValueStore.SetInternalNode(internalNodeKey, internalNodeValue);
    }
    public void InsertBatch(long blockNumber, VerkleMemoryDb batch) { }

    public void ApplyDiffLayer(BatchChangeSet changeSet) { }

    public Hash256 GetStateRoot()
    {
        return _verkleStateStore.GetStateRoot();
    }

    public bool HashStateForBlock(Hash256 stateRoot)
    {
        return _verkleStateStore.HashStateForBlock(stateRoot);
    }

    public bool MoveToStateRoot(Hash256 stateRoot)
    {
        return _verkleStateStore.MoveToStateRoot(stateRoot);
    }

#pragma warning disable 67
    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached;
#pragma warning restore 67

    public ReadOnlyVerkleStateStore AsReadOnly(VerkleMemoryDb keyValueStore)
    {
        return new ReadOnlyVerkleStateStore(_verkleStateStore, keyValueStore);
    }
    public bool IsFullySynced(Hash256 stateRoot)
    {
        return _verkleStateStore.IsFullySynced(stateRoot);
    }

    public IEnumerable<KeyValuePair<byte[], byte[]>> GetLeafRangeIterator(byte[] fromRange, byte[] toRange, long blockNumber)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, blockNumber);
    }

    public IEnumerable<PathWithSubTree> GetLeafRangeIterator(Stem fromRange, Stem toRange, Hash256 stateRoot, long bytes)
    {
        return _verkleStateStore.GetLeafRangeIterator(fromRange, toRange, stateRoot, bytes);
    }
}

