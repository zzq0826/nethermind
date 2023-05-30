// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Verkle.Tree.Sync;

public class PathWithSubTree
{
    public PathWithSubTree(byte[] stem, LeafInSubTree[] subTree)
    {
        Path = stem;
        SubTree = subTree;
    }

    public byte[]  Path { get; set; }
    public LeafInSubTree[]  SubTree { get; set; }
}

public readonly struct LeafInSubTree
{
    public readonly byte SuffixByte;
    public readonly byte[] Leaf;

    public LeafInSubTree(byte suffixByte, byte[] leaf)
    {
        SuffixByte = suffixByte;
        Leaf = leaf;
    }

    public static implicit operator LeafInSubTree((byte, byte[]) leafWithSubIndex)
    {
        return new LeafInSubTree(leafWithSubIndex.Item1, leafWithSubIndex.Item2);
    }
}
