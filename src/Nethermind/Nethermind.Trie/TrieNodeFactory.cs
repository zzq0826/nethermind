// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Xml.XPath;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie
{
    internal static class TrieNodeFactory
    {
        public static TrieNode CreateBranch()
        {
            TrieNode node = new(NodeType.Branch);
            return node;
        }

        public static TrieNode CreateLeaf(byte[] path, byte[]? value)
        {
            TrieNode node = new(NodeType.Leaf);
            node.Key = path;
            node.Value = value;
            return node;
        }

        public static TrieNode CreateHashLeaf(byte[] path, Keccak? value)
        {
            TrieNode node = new(NodeType.Leaf);
            node.Key = path;
            node.Keccak = value;
            return node;
        }

        public static TrieNode CreateExtension(byte[] path)
        {
            TrieNode node = new(NodeType.Extension);
            node.Key = path;
            return node;
        }

        public static TrieNode CreateExtension(byte[] path, TrieNode child)
        {
            TrieNode node = new(NodeType.Extension);
            node.SetChild(0, child);
            node.Key = path;
            return node;
        }
    }
}
