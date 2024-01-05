// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Extensions;
using Nethermind.Core.Verkle;

namespace Nethermind.Verkle.Tree.Sync;

public class PathWithSubTree
{
    public PathWithSubTree(Stem stem, LeafInSubTree[] subTree)
    {
        Path = stem;
        SubTree = subTree;
    }

    public Stem Path { get; set; }
    public LeafInSubTree[] SubTree { get; set; }
}

public readonly struct LeafInSubTree: IEquatable<LeafInSubTree>
{
    public readonly byte SuffixByte;
    public readonly byte[]? Leaf;

    public LeafInSubTree(byte suffixByte, byte[]? leaf)
    {
        SuffixByte = suffixByte;
        Leaf = leaf;
    }

    public static implicit operator LeafInSubTree((byte, byte[]) leafWithSubIndex)
    {
        return new LeafInSubTree(leafWithSubIndex.Item1, leafWithSubIndex.Item2);
    }

    public static implicit operator LeafInSubTree(KeyValuePair<byte, byte[]> leafWithSubIndex)
    {
        return new LeafInSubTree(leafWithSubIndex.Key, leafWithSubIndex.Value);
    }

    public bool Equals(LeafInSubTree other)
    {
        if (other.SuffixByte != SuffixByte) return false;
        if (other.Leaf is null) return Leaf is null;
        return Leaf is not null && other.Leaf.SequenceEqual(Leaf);
    }

    public override string ToString()
    {
        return $"{SuffixByte}:{Leaf?.ToHexString()}";
    }

    public override bool Equals(object obj)
    {
        return obj is LeafInSubTree && Equals((LeafInSubTree)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(SuffixByte, Leaf);
    }
}
