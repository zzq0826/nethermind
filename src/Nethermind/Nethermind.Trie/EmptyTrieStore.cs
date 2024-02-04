// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Trie.Pruning;

namespace Nethermind.Trie;

public class EmptyTrieStore: ITrieStore, IReadOnlyTrieStore
{
    public static ITrieStore Instance = new EmptyTrieStore();
    private static TrieNode EmptyTreeRoot = new TrieNode(NodeType.Leaf, Keccak.EmptyTreeHash, new byte[] { 128 });

    public TrieNode FindCachedOrUnknown(Hash256 hash)
    {
        if (hash == Keccak.EmptyTreeHash) return EmptyTreeRoot;
        return new TrieNode(NodeType.Unknown, hash);
    }

    public byte[]? LoadRlp(Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        if (hash == Keccak.EmptyTreeHash) return EmptyTreeRoot.FullRlp.ToArray();
        throw new TrieException("plain trie store does not store any rlp");
    }

    public byte[]? TryLoadRlp(Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        if (hash == Keccak.EmptyTreeHash) return EmptyTreeRoot.FullRlp.ToArray();
        return null;
    }

    public void Dispose()
    {
    }

    public void CommitNode(long blockNumber, NodeCommitInfo nodeCommitInfo, WriteFlags writeFlags = WriteFlags.None)
    {
    }

    public void FinishBlockCommit(TrieType trieType, long blockNumber, TrieNode? root, WriteFlags writeFlags = WriteFlags.None)
    {
    }

    public bool IsPersisted(in ValueHash256 keccak)
    {
        return keccak == Keccak.EmptyTreeHash;
    }

    public IReadOnlyTrieStore AsReadOnly(IKeyValueStore? keyValueStore)
    {
        return this;
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add { }
        remove { }
    }

    public IReadOnlyKeyValueStore TrieNodeRlpStore => NullKeyValueStore.Instance;
    public void Set(in ValueHash256 hash, byte[] rlp)
    {
    }
}
