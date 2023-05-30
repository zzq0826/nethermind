// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Core.Collections;
using Nethermind.Core.Extensions;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Proofs;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree
{
    private void InsertBranchNodeForSync(byte[] path, Commitment commitment)
    {
        InternalNode node = VerkleNodes.CreateStatelessBranchNode(commitment);
        _verkleStateStore.SetInternalNode(path, node);
    }

    private void InsertSubTreesForSync(Dictionary<byte[], (byte, byte[])[]> subTrees)
    {
        Span<byte> key = new byte[32];
        foreach (KeyValuePair<byte[], (byte, byte[])[]> subTree in subTrees)
        {
            subTree.Key.CopyTo(key);
            LeafUpdateDelta leafUpdateDelta = new();
            foreach ((byte, byte[]) leafs in subTree.Value)
            {
                key[31] = leafs.Item1;
                leafUpdateDelta.UpdateDelta(GetLeafDelta(leafs.Item2, leafs.Item1), leafs.Item1);
                _verkleStateStore.SetLeaf(key.ToArray(), leafs.Item2);
            }
            _leafUpdateCache[subTree.Key] = leafUpdateDelta;
        }
    }

    private bool VerifyCommitmentThenInsertStem(byte[] pathOfStem, byte[] stem, Commitment expectedCommitment)
    {
        InternalNode stemNode = VerkleNodes.CreateStatelessStemNode(stem);
        stemNode.UpdateCommitment(_leafUpdateCache[stem]);
        if (stemNode.InternalCommitment.Point != expectedCommitment.Point) return false;
        _verkleStateStore.SetInternalNode(pathOfStem, stemNode);
        return true;
    }

    private void InsertPlaceholderForNotPresentStem(Span<byte> stem, byte[] pathOfStem, Commitment stemCommitment)
    {
        InternalNode stemNode = VerkleNodes.CreateStatelessStemNode(stem.ToArray(), stemCommitment);
        _verkleStateStore.SetInternalNode(pathOfStem, stemNode);
    }

    private void InsertStemBatchForSync(Dictionary<byte[], List<byte[]>> stemBatch,
        IDictionary<List<byte>, Banderwagon> commByPath)
    {
        foreach (KeyValuePair<byte[], List<byte[]>> prefixWithStem in stemBatch)
        {
            foreach (byte[] stem in prefixWithStem.Value)
            {
                TraverseContext context = new(stem, _leafUpdateCache[stem])
                    { CurrentIndex = prefixWithStem.Key.Length - 1 };
                TraverseBranch(context);
            }

            commByPath[new List<byte>(prefixWithStem.Key)] = _verkleStateStore.GetInternalNode(prefixWithStem.Key)!
                .InternalCommitment.Point;
        }
    }
    // public bool InsertIntoStatelessTree(VerkleProof proof, List<byte[]> keys, List<byte[]?> values, Banderwagon root)
    // {
    //     (bool, UpdateHint?) verification = Verify(proof, keys, values, root);
    //     if (!verification.Item1) return false;
    //
    //     InsertAfterVerification(verification.Item2!.Value, keys, values, root, false);
    //     return true;
    // }

    // public void InsertAfterVerification(UpdateHint hint, List<byte[]> keys, List<byte[]?> values, Banderwagon root, bool skipRoot = true)
    // {
    //     if (!skipRoot)
    //     {
    //         InternalNode rootNode = new(VerkleNodeType.BranchNode, new Commitment(root));
    //         _verkleStateStore.SetInternalNode(Array.Empty<byte>(), rootNode);
    //     }
    //
    //     AddStatelessInternalNodes(hint);
    //
    //     for (int i = 0; i < keys.Count; i++)
    //     {
    //         byte[]? value = values[i];
    //         if(value is null) continue;
    //         _verkleStateStore.SetLeaf(keys[i], value);
    //     }
    // }

    public void AddStatelessInternalNodes(UpdateHint hint, Dictionary<byte[], LeafUpdateDelta> subTrees)
    {
        List<byte> pathList = new();
        foreach ((byte[]? stem, (ExtPresent extStatus, byte depth)) in hint.DepthAndExtByStem)
        {
            pathList.Clear();
            for (int i = 0; i < depth - 1; i++)
            {
                pathList.Add(stem[i]);
                InternalNode node = VerkleNodes.CreateStatelessBranchNode(new Commitment(hint.CommByPath[pathList]));
                _verkleStateStore.SetInternalNode(pathList.ToArray(), node);
            }

            pathList.Add(stem[depth-1]);

            InternalNode stemNode;
            byte[] pathOfStem;
            switch (extStatus)
            {
                case ExtPresent.None:
                    stemNode = VerkleNodes.CreateStatelessStemNode(stem, new Commitment());
                    pathOfStem = pathList.ToArray();
                    break;
                case ExtPresent.DifferentStem:
                    byte[] otherStem = hint.DifferentStemNoProof[pathList];
                    Commitment otherInternalCommitment = new(hint.CommByPath[pathList]);
                    stemNode = VerkleNodes.CreateStatelessStemNode(otherStem, otherInternalCommitment);
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

                    stemNode = VerkleNodes.CreateStatelessStemNode(stem, c1, c2, internalCommitment);
                    pathOfStem = new byte[pathList.Count - 1];
                    pathList.CopyTo(0, pathOfStem, 0, pathList.Count - 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            _verkleStateStore.SetInternalNode(pathOfStem, stemNode);
        }
    }

    public static bool CreateStatelessTreeFromRange(IVerkleStore store, VerkleProof proof, Banderwagon rootPoint, byte[] startStem, byte[] endStem, Dictionary<byte[], (byte, byte[])[]> subTrees)
    {
        const int numberOfStems = 2;
        List<Banderwagon> commSortedByPath = new(proof.CommsSorted.Length + 1) { rootPoint };
        commSortedByPath.AddRange(proof.CommsSorted);

        byte[][] stems = { startStem, endStem };

        // map stems to depth and extension status and create a list of stem with extension present
        Dictionary<byte[], (ExtPresent, byte)> depthsAndExtByStem = new(Bytes.EqualityComparer);
        HashSet<byte[]> stemsWithExtension = new(Bytes.EqualityComparer);
        for (int i = 0; i < numberOfStems; i++)
        {
            ExtPresent extPresent = proof.VerifyHint.ExtensionPresent[i];
            depthsAndExtByStem.Add(stems[i], (extPresent, proof.VerifyHint.Depths[i]));
            if (extPresent == ExtPresent.Present) stemsWithExtension.Add(stems[i]);
        }

        SortedSet<List<byte>> allPaths = new(new ListComparer());
        SortedSet<(List<byte>, byte)> allPathsAndZs = new(new ListWithByteComparer());
        Dictionary<(List<byte>, byte), FrE> leafValuesByPathAndZ = new(new ListWithByteEqualityComparer());
        SortedDictionary<List<byte>, byte[]> otherStemsByPrefix = new(new ListComparer());

        int prefixLength = 0;
        while (prefixLength<startStem.Length)
        {
            if (startStem[prefixLength] != endStem[prefixLength]) break;
            prefixLength++;
        }

        int keyIndex = 0;
        foreach (byte[] stem in stems)
        {
            (ExtPresent extPres, byte depth) = depthsAndExtByStem[stem];

            for (int i = 0; i < depth; i++)
            {
                allPaths.Add(new List<byte>(stem[..i]));
                if (i < prefixLength)
                {
                    allPathsAndZs.Add((new List<byte>(stem[..i]), stem[i]));
                    continue;
                }
                int startIndex = startStem[i];
                int endIndex = endStem[i];
                if (i > prefixLength)
                {
                    if (keyIndex == 0) endIndex = 255;
                    else startIndex = 0;
                }

                for (int j = startIndex; j <= endIndex; j++)
                {
                    allPathsAndZs.Add((new List<byte>(stem[..i]), (byte)j));
                }
            }

            switch (extPres)
            {
                case ExtPresent.DifferentStem:

                    allPaths.Add(new List<byte>(stem[..depth]));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 0));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 1));

                    byte[] otherStem;

                    // find the stems that are equal to the stem we are assuming to be without extension
                    // this happens when we initially added this stem when we were searching for another one
                    // but then in a future key, we found that we needed this stem too.
                    byte[][] found = stemsWithExtension.Where(x => x[..depth].SequenceEqual(stem[..depth])).ToArray();

                    switch (found.Length)
                    {
                        case 0:
                            found = proof.VerifyHint.DifferentStemNoProof.Where(x => x[..depth].SequenceEqual(stem[..depth])).ToArray();
                            byte[] encounteredStem = found[^1];
                            otherStem = encounteredStem;

                            // Add extension node to proof in particular, we only want to open at (1, stem)
                            leafValuesByPathAndZ[(new List<byte>(stem[..depth]), 0)] = FrE.One;
                            leafValuesByPathAndZ.Add((new List<byte>(stem[..depth]), 1), FrE.FromBytesReduced(encounteredStem.Reverse().ToArray()));
                            break;
                        case 1:
                            otherStem = found[0];
                            break;
                        default:
                            throw new InvalidDataException($"found more than one instance of stem_with_extension at depth {depth}, see: {string.Join(" | ", found.Select(x => string.Join(", ", x)))}");
                    }

                    otherStemsByPrefix.Add(stem[..depth].ToList(), otherStem);
                    break;
                case ExtPresent.Present:
                    allPaths.Add(new List<byte>(stem[..depth]));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 0));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 1));

                    leafValuesByPathAndZ[(new List<byte>(stem[..depth]), 0)] = FrE.One;
                    leafValuesByPathAndZ[(new List<byte>(stem[..depth]), 1)] = FrE.FromBytesReduced(stem.Reverse().ToArray());
                    break;
                case ExtPresent.None:
                    leafValuesByPathAndZ[depth == 1 ? (new List<byte>(), stem[depth - 1]) : (stem[..depth].ToList(), stem[depth - 1])] = FrE.Zero;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            keyIndex++;
        }

        Dictionary<List<byte>, Banderwagon> commByPath = new(new ListEqualityComparer());
        foreach ((List<byte> path, Banderwagon comm) in allPaths.Zip(commSortedByPath))
        {
            commByPath[path] = comm;
        }

        VerkleTree tree = new(store);

        byte[][] stemsWithoutStartAndEndStems =
            subTrees.Keys.Where(x => !x.SequenceEqual(startStem) && !x.SequenceEqual(endStem)).ToArray();

        HashSet<byte[]> subTreesToCreate = UpdatePathsAndReturnSubTreesToCreate(allPaths, allPathsAndZs, stemsWithoutStartAndEndStems);
        tree.InsertSubTreesForSync(subTrees);

        List<byte> pathList = new();
        foreach ((byte[]? stem, (ExtPresent extStatus, byte depth)) in depthsAndExtByStem)
        {
            pathList.Clear();
            for (int i = 0; i < depth - 1; i++)
            {
                pathList.Add(stem[i]);
                tree.InsertBranchNodeForSync(pathList.ToArray(), new Commitment(commByPath[pathList]));
            }

            pathList.Add(stem[depth-1]);

            switch (extStatus)
            {
                case ExtPresent.None:
                    tree.InsertPlaceholderForNotPresentStem(stem, pathList.ToArray(), new Commitment());
                    break;
                case ExtPresent.DifferentStem:
                    byte[] otherStem = otherStemsByPrefix[pathList];
                    tree.InsertPlaceholderForNotPresentStem(otherStem, pathList.ToArray(), new(commByPath[pathList]));
                    break;
                case ExtPresent.Present:
                    Commitment internalCommitment = new(commByPath[pathList]);
                    if (!tree.VerifyCommitmentThenInsertStem(pathList.ToArray(), stem, internalCommitment))
                        return false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        byte[][] allStemsWithoutStartAndEndStems =
            subTrees.Keys.Where(x => !x.SequenceEqual(startStem) && !x.SequenceEqual(endStem)).ToArray();

        int stemIndex = 0;
        Dictionary<byte[], List<byte[]>> stemBatch = new(Bytes.EqualityComparer);
        foreach (byte[] stemPrefix in subTreesToCreate)
        {
            stemBatch.Add(stemPrefix, new List<byte[]>());
            while (stemIndex < allStemsWithoutStartAndEndStems.Length)
            {
                if (Bytes.EqualityComparer.Equals(stemPrefix, allStemsWithoutStartAndEndStems[stemIndex][..stemPrefix.Length]))
                {
                    stemBatch[stemPrefix].Add(allStemsWithoutStartAndEndStems[stemIndex]);
                    stemIndex++;
                }
                else break;
            }
        }

        tree.InsertStemBatchForSync(stemBatch, commByPath);
        bool verification = VerifyVerkleProofStruct(proof.Proof, allPathsAndZs, leafValuesByPathAndZ, commByPath);
        if (!verification) tree.Reset();
        else tree.CommitTree(0);

        return verification;
    }

    private static HashSet<byte[]> UpdatePathsAndReturnSubTreesToCreate(IReadOnlySet<List<byte>> allPaths,
        ISet<(List<byte>, byte)> allPathsAndZs, IEnumerable<byte[]> stems)
    {
        HashSet<byte[]> subTreesToCreate = new(Bytes.EqualityComparer);
        foreach (byte[] stem in stems)
        {
            for (int i = 0; i < 32; i++)
            {
                List<byte> prefix = new(stem[..i]);
                if (allPaths.Contains(prefix))
                {
                    allPathsAndZs.Add((prefix, stem[i]));
                }
                else
                {
                    subTreesToCreate.Add(prefix.ToArray());
                    break;
                }
            }
        }

        return subTreesToCreate;
    }
}
