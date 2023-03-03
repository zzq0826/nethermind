// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;

namespace Nethermind.Trie
{
    internal static class TrieNodeFactory
    {
        public static TrieNode CreateBranch(byte[]? pathToNode = null, byte[]? storagePrefix = null)
        {
            TrieNode node = new TrieNode(NodeType.Branch);
            if (pathToNode is not null) node.PathToNode = pathToNode;
            if (storagePrefix is not null) node.StorePrefix = storagePrefix;
            return node;
        }

        public static TrieNode CreateLeaf(byte[] path, byte[]? value, byte[]? pathToNode = null, byte[]? storagePrefix = null)
        {
            TrieNode node = new TrieNode(NodeType.Leaf)
            {
                Key = path,
                Value = value
            };
            if (storagePrefix is not null) node.StorePrefix = storagePrefix;
            if (pathToNode is null) return node;
            Debug.Assert(path.Length + pathToNode.Length == 64);
            node.PathToNode = pathToNode;
            return node;
        }

        public static TrieNode CreateExtension(byte[] path, byte[]? pathToNode = null, byte[]? storagePrefix = null)
        {
            TrieNode node = new TrieNode(NodeType.Extension)
            {
                Key = path
            };
            if (pathToNode is not null) node.PathToNode = pathToNode;
            if (storagePrefix is not null) node.StorePrefix = storagePrefix;
            return node;
        }

        public static TrieNode CreateExtension(byte[] path, TrieNode child, byte[]? pathToNode = null, byte[]? storagePrefix = null)
        {
            TrieNode node = new TrieNode(NodeType.Extension);
            node.SetChild(0, child);
            node.Key = path;
            if (pathToNode is not null) node.PathToNode = pathToNode;
            if (storagePrefix is not null) node.StorePrefix = storagePrefix;
            return node;
        }

        public static TrieNode CreateBranch(Span<byte> pathToNode, byte[]? storagePrefix) => CreateBranch(pathToNode.ToArray(), storagePrefix);
        public static TrieNode CreateLeaf(byte[] path, byte[]? value, Span<byte> pathToNode, byte[]? storagePrefix) => CreateLeaf(path, value, pathToNode.ToArray(), storagePrefix);
        public static TrieNode CreateExtension(byte[] path, Span<byte> pathToNode, byte[]? storagePrefix) => CreateExtension(path, pathToNode.ToArray(), storagePrefix);
        public static TrieNode CreateExtension(byte[] path, TrieNode child, Span<byte> pathToNode, byte[]? storagePrefix) => CreateExtension(path, child, pathToNode.ToArray(), storagePrefix);
    }
}
