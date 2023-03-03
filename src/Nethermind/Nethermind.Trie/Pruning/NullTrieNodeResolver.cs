// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Pruning
{
    public class NullTrieNodeResolver : ITrieNodeResolver
    {
        public TrieNodeResolverCapability Capability => TrieNodeResolverCapability.Hash;
        private NullTrieNodeResolver() { }

        public static readonly NullTrieNodeResolver Instance = new();

        public TrieNode FindCachedOrUnknown(Keccak hash) => new TrieNode(NodeType.Unknown, hash);

        public byte[]? LoadRlp(Keccak hash) => null;

        public TrieNode FindCachedOrUnknown(Span<byte> nodePath) => null;

        public byte[]? LoadRlp(Span<byte> nodePath, Keccak rootHash) => null;
    }
}
