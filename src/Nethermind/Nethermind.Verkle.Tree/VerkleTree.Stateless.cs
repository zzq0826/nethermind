// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Proofs;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree
{
    public void AddStatelessNode(byte[] pathNode, InternalNode node)
    {
        _verkleStateStore.SetInternalNode(pathNode, node);
    }

    public void AddStatelessLeafNode(byte[] leafPath, byte[] leaf)
    {
        _verkleStateStore.SetLeaf(leafPath, leaf);
    }

    public void TreeFromProofs(VerkleProof proof, List<byte[]> keys, List<byte[]?> values, Banderwagon root)
    {
        HashSet<byte[]> stem = new HashSet<byte[]>();

        foreach (byte[] key in keys)
        {
            stem.Add(key[..31]);
        }

        int stemIndex;
        var info = new Dictionary<byte[], StemInfo>();

        // for (int i = 0; i < proof; i++)
        // {
        //
        // }




    }

    private readonly struct StemInfo
    {
        public readonly byte Depth;
        public readonly byte StemType;
        public readonly bool HasC1;
        public readonly bool HasC2;
        public readonly Dictionary<byte, byte[]> Values;
        public readonly byte[] Stem;
    }


}
