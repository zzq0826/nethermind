// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Proofs;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree
{

    public bool InsertIntoStatelessTree(VerkleProof proof, List<byte[]> keys, List<byte[]?> values, Banderwagon root)
    {
        (bool, UpdateHint?) verification = Verify(proof, keys, values, root);
        if (!verification.Item1) return false;

        InsertAfterVerification(verification.Item2!.Value, keys, values, root, false);
        return true;
    }


    private void InsertAfterVerification(UpdateHint hint, List<byte[]> keys, List<byte[]?> values, Banderwagon root, bool skipRoot = true)
    {
        if (!skipRoot)
        {
            InternalNode rootNode = new(VerkleNodeType.BranchNode, new Commitment(root));
            _verkleStateStore.SetInternalNode(Array.Empty<byte>(), rootNode);
        }

        AddStatelessInternalNodes(hint);

        for (int i = 0; i < keys.Count; i++)
        {
            byte[]? value = values[i];
            if(value is null) continue;
            _verkleStateStore.SetLeaf(keys[i], value);
        }
    }

    private void AddStatelessInternalNodes(UpdateHint hint)
    {
        List<byte> pathList = new();
        foreach ((byte[]? stem, (ExtPresent extStatus, byte depth)) in hint.DepthAndExtByStem)
        {
            pathList.Clear();
            for (int i = 0; i < depth - 1; i++)
            {
                pathList.Add(stem[i]);
                InternalNode node = new(VerkleNodeType.BranchNode, new Commitment(hint.CommByPath[pathList]));
                node.IsStateless = true;
                _verkleStateStore.SetInternalNode(pathList.ToArray(), node);
            }

            pathList.Add(stem[depth-1]);

            InternalNode stemNode;
            byte[] pathOfStem;
            switch (extStatus)
            {
                case ExtPresent.None:
                    stemNode =  new(VerkleNodeType.StemNode, stem, null, null, new Commitment());
                    pathOfStem = pathList.ToArray();
                    break;
                case ExtPresent.DifferentStem:
                    byte[] otherStem = hint.DifferentStemNoProof[pathList];
                    Commitment otherInternalCommitment = new(hint.CommByPath[pathList]);
                    stemNode = new(VerkleNodeType.StemNode, otherStem, null, null, otherInternalCommitment);
                    pathOfStem = pathList.ToArray();
                    break;
                case ExtPresent.Present:
                    Commitment internalCommitment = new(hint.CommByPath[pathList]);
                    Commitment? c1 = null;
                    Commitment? c2 = null;

                    pathList.Add(2);
                    if (hint.CommByPath.TryGetValue(pathList, out Banderwagon c1B)) c1 = new Commitment(c1B);
                    pathList[^1] = 3;
                    if (hint.CommByPath.TryGetValue(pathList, out Banderwagon c2B)) c2 = new Commitment(c2B);

                    stemNode = new(VerkleNodeType.StemNode, stem, c1, c2, internalCommitment);
                    pathOfStem = new byte[pathList.Count - 1];
                    pathList.CopyTo(0, pathOfStem, 0, pathList.Count - 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _verkleStateStore.SetInternalNode(pathOfStem, stemNode);
        }
    }
}
