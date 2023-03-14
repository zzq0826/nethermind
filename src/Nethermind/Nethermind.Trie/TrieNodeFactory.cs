// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;

namespace Nethermind.Trie
{
    internal static class TrieNodeFactory
    {
        public static TrieNode CreateBranch(byte[]? pathToNode = null)
        {
            TrieNode node = new TrieNode(NodeType.Branch);
            if (pathToNode is not null) node.PathToNode = pathToNode;
            return node;
        }

        public static TrieNode CreateLeaf(byte[] path, byte[]? value, byte[]? pathToNode = null)
        {
            TrieNode node = new TrieNode(NodeType.Leaf)
            {
                Key = path,
                Value = value
            };
            if (pathToNode is null) return node;
            node.PathToNode = pathToNode;
            if (node.Key.Length + node.PathToNode.Length != 64)
                throw new Exception("what?");
            return node;
        }

        public static TrieNode CreateExtension(byte[] path, byte[]? pathToNode = null)
        {
            TrieNode node = new TrieNode(NodeType.Extension)
            {
                Key = path
            };
            if (pathToNode is not null) node.PathToNode = pathToNode;
            return node;
        }

        public static TrieNode CreateExtension(byte[] path, TrieNode child, byte[]? pathToNode = null)
        {
            TrieNode node = new TrieNode(NodeType.Extension);
            node.SetChild(0, child);
            node.Key = path;
            if (pathToNode is not null) node.PathToNode = pathToNode;
            return node;
        }

        public static TrieNode CreateBranch(Span<byte> pathToNode) => CreateBranch(pathToNode.ToArray());
        public static TrieNode CreateLeaf(byte[] path, byte[]? value, Span<byte> pathToNode) => CreateLeaf(path, value, pathToNode.ToArray());
        public static TrieNode CreateExtension(byte[] path, Span<byte> pathToNode) => CreateExtension(path, pathToNode.ToArray());
        public static TrieNode CreateExtension(byte[] path, TrieNode child, Span<byte> pathToNode) => CreateExtension(path, child, pathToNode.ToArray());
    }
}
