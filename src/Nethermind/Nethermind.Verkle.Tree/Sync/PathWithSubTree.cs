// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Verkle.Tree.Sync;

public class PathWithSubTree
{
    public PathWithSubTree(byte[] stem, byte[][] subTree)
    {
        Path = stem;
        SubTree = subTree;
    }

    public byte[]  Path { get; set; }
    public byte[][]  SubTree { get; set; }
}
