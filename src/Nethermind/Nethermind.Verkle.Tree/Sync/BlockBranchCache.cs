// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.ClearScript.Util.Web;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;
using Org.BouncyCastle.Utilities;
using Bytes = Nethermind.Core.Extensions.Bytes;

namespace Nethermind.Verkle.Tree.Sync;

public struct StateInfo(ReadOnlyVerkleMemoryDb stateDiff, Hash256 stateRoot, long blockNumber)
{
    public ReadOnlyVerkleMemoryDb StateDiff = stateDiff;
    public Hash256 StateRoot = stateRoot;
    public long BlockNumber = blockNumber;
}

public class BlockBranchNode(ReadOnlyVerkleMemoryDb stateDiff, Hash256 stateRoot, long blockNumber)
{
    public BlockBranchNode? ParentNode;
    public StateInfo Data = new(stateDiff, stateRoot, blockNumber);
}

public class BlockBranchCache(int cacheSize) : BlockBranchLinkedList(cacheSize)
{
    public byte[]? GetLeaf(byte[] key, Hash256 stateRoot)
    {
        BlockBranchEnumerator diffs = GetEnumerator(stateRoot);
        while (diffs.MoveNext())
        {
            if (diffs.Current.Data.StateDiff.LeafTable.TryGetValue(key.ToArray(), out byte[]? node)) return node;
        }
        return null;
    }

    public InternalNode? GetInternalNode(byte[] key, Hash256 stateRoot)
    {
        BlockBranchEnumerator diffs = GetEnumerator(stateRoot);
        while (diffs.MoveNext())
        {
            if (diffs.Current.Data.StateDiff.InternalTable.TryGetValue(key, out InternalNode? node)) return node!.Clone();
        }
        return null;
    }

}

public class BlockBranchLinkedList(int cacheSize)
{
    private readonly SpanDictionary<byte, BlockBranchNode> _stateRootToNodeMapping = new(Bytes.SpanEqualityComparer);

    private BlockBranchNode? _lastNode;
    private int _version = 0;

    public bool AddNode(long blockNumber, Hash256 stateRoot, ReadOnlyVerkleMemoryDb data, Hash256 parentRoot, [MaybeNullWhen(false)]out BlockBranchNode outNode)
    {
        var node = new BlockBranchNode(data, stateRoot, blockNumber);
        // this means that the list is empty
        if (_lastNode is null)
        {
            _stateRootToNodeMapping[stateRoot.Bytes] = outNode = _lastNode = node;
            return true;
        }
        BlockBranchNode? parentNode = _stateRootToNodeMapping[parentRoot.Bytes];
        node.ParentNode = parentNode;
        outNode = _stateRootToNodeMapping[stateRoot.Bytes] = node;
        _version++;
        return true;
    }

    public bool EnqueueAndReplaceIfFull(long blockNumber, Hash256 stateRoot, ReadOnlyVerkleMemoryDb data, Hash256 parentRoot, [MaybeNullWhen(false)]out BlockBranchNode node)
    {
        if (!AddNode(blockNumber, stateRoot, data, parentRoot, out BlockBranchNode insertedNode)) throw new Exception("This is a error");

        if (_lastNode is not null)
        {
            if (blockNumber - _lastNode.Data.BlockNumber > cacheSize)
            {
                BlockBranchNode? currentNode = insertedNode;
                while (currentNode.ParentNode!.ParentNode is not null)
                {
                    currentNode = currentNode.ParentNode;
                }
                _lastNode = currentNode;
                node = currentNode.ParentNode;
                _stateRootToNodeMapping.Remove(node.Data.StateRoot.Bytes);
                currentNode.ParentNode = null;
                return true;
            }
        }

        node = null;
        return false;
    }

    public bool GetStateRootNode(Hash256 stateRoot, [MaybeNullWhen(false)]out BlockBranchNode node)
    {
        return _stateRootToNodeMapping.TryGetValue(stateRoot.Bytes, out node);
    }

    public BlockBranchEnumerator GetEnumerator(Hash256 stateRoot)
    {
        return new BlockBranchEnumerator(this, stateRoot);
    }

    public struct BlockBranchEnumerator : IEnumerator
    {
        private readonly Hash256 _startingStateRoot;
        private BlockBranchNode? _node;
        private readonly BlockBranchLinkedList _cache;
        private int _version;
        private BlockBranchNode? _currentElement;

        public BlockBranchEnumerator(BlockBranchLinkedList cache, Hash256 startingStateRoot)
        {
            _startingStateRoot = startingStateRoot;
            _cache = cache;
            _version = cache._version;
            _cache.GetStateRootNode(_startingStateRoot, out _node);
            _currentElement = default;
        }
        public bool MoveNext()
        {
            if (this._version != _cache._version) throw new Exception("Should be same version");
            if (_node == null) return false;
            _currentElement = _node;
            _node = _node.ParentNode;
            return true;
        }

        public void Reset()
        {
            _version = _cache._version;
            _currentElement = default;
            _cache.GetStateRootNode(_startingStateRoot, out _node);

        }
        public BlockBranchNode Current => _currentElement!;
        object IEnumerator.Current => _currentElement!;
    }
}



