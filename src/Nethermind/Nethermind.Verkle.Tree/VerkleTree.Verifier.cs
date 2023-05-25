// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Nethermind.Core.Extensions;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Polynomial;
using Nethermind.Verkle.Proofs;
using Nethermind.Verkle.Tree.Proofs;

namespace Nethermind.Verkle.Tree;

public partial class VerkleTree
{
    private static byte[][] GetStemsFromKeys(Span<byte[]> keys, int numberOfStems)
    {
        byte[][] stems = new byte[numberOfStems][];
        int stemIndex = 1;
        stems[0] = keys[0][..31];
        IEqualityComparer<byte[]> comparer = Bytes.EqualityComparer;
        for (int i = 1; i < numberOfStems; i++)
        {
            byte[] currentStem = keys[i][..31];
            if(comparer.Equals(stems[stemIndex-1], currentStem)) continue;
            stems[stemIndex++] = currentStem;
        }
        return stems;
    }

    public static bool VerifyVerkleProof(VerkleProof proof, List<byte[]> keys, List<byte[]?> values, Banderwagon root, [NotNullWhen(true)]out UpdateHint? updateHint)
    {
        updateHint = null;

        int numberOfStems = proof.VerifyHint.Depths.Length;

        // sorted commitments including root
        List<Banderwagon> commSortedByPath = new(proof.CommsSorted.Length + 1) { root };
        commSortedByPath.AddRange(proof.CommsSorted);

        byte[][] stems = GetStemsFromKeys(CollectionsMarshal.AsSpan(keys), numberOfStems);

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
        foreach ((byte[] key, byte[]? value) in keys.Zip(values))
        {
            byte[] stem = key[..31];
            (ExtPresent extPres, byte depth) = depthsAndExtByStem[stem];

            for (int i = 0; i < depth; i++)
            {
                allPaths.Add(new List<byte>(stem[..i]));
                allPathsAndZs.Add((new List<byte>(stem[..i]), stem[i]));
            }

            switch (extPres)
            {
                case ExtPresent.DifferentStem:

                    allPaths.Add(new List<byte>(stem[..depth]));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 0));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 1));

                    // since the stem was different - value should not have been set
                    if (value != null) return false;

                    Debug.Assert(depth != stem.Length);

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

                    byte suffix = key[31];
                    byte openingIndex = suffix < 128 ? (byte)2 : (byte)3;

                    allPathsAndZs.Add((new List<byte>(stem[..depth]), openingIndex));


                    // this should definitely be the stem + openingIndex, but the path is just used for sorting
                    // and indexing the values - this is directly never used for verification
                    // so it is a good idea to used values as small as possible without the issues of collision
                    List<byte> suffixTreePath = new(stem[..depth]) { openingIndex };

                    allPaths.Add(new List<byte>(suffixTreePath.ToArray()));
                    byte valLowerIndex = (byte)(2 * (suffix % 128));
                    byte valUpperIndex = (byte)(valLowerIndex + 1);

                    allPathsAndZs.Add((new List<byte>(suffixTreePath.ToArray()), valLowerIndex));
                    allPathsAndZs.Add((new List<byte>(suffixTreePath.ToArray()), valUpperIndex));

                    (FrE valLow, FrE valHigh) = VerkleUtils.BreakValueInLowHigh(value);

                    leafValuesByPathAndZ[(new List<byte>(suffixTreePath.ToArray()), valLowerIndex)] = valLow;
                    leafValuesByPathAndZ[(new List<byte>(suffixTreePath.ToArray()), valUpperIndex)] = valHigh;
                    break;
                case ExtPresent.None:
                    // If the extension was not present, then the value should be None
                    if (value != null) return false;

                    leafValuesByPathAndZ[depth == 1 ? (new List<byte>(), stem[depth - 1]) : (stem[..depth].ToList(), stem[depth - 1])] = FrE.Zero;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Debug.Assert(commSortedByPath.Count == allPaths.Count);

        Dictionary<List<byte>, Banderwagon> commByPath = new(new ListEqualityComparer());
        foreach ((List<byte> path, Banderwagon comm) in allPaths.Zip(commSortedByPath))
        {
            commByPath[path] = comm;
        }

        bool isTrue = VerifyVerkleProofStruct(proof.Proof, allPathsAndZs, leafValuesByPathAndZ, commByPath);
        updateHint = new UpdateHint
        {
            DepthAndExtByStem = depthsAndExtByStem,
            CommByPath = commByPath,
            DifferentStemNoProof = otherStemsByPrefix
        };

        return isTrue;
    }

    private static bool VerifyVerkleProofStruct(VerkleProofStruct proof, SortedSet<(List<byte>, byte)> allPathsAndZs, Dictionary<(List<byte>, byte), FrE> leafValuesByPathAndZ,  Dictionary<List<byte>, Banderwagon> commByPath)
    {
        Banderwagon[] comms = new Banderwagon[allPathsAndZs.Count];
        int index = 0;
        foreach ((List<byte> path, byte z) in allPathsAndZs)
        {
            comms[index++] = commByPath[path];
        }

        Console.WriteLine("ysByPathAndZ");
        SortedDictionary<(List<byte>, byte), FrE> ysByPathAndZ = new(new ListWithByteComparer());
        foreach ((List<byte> path, byte z) in allPathsAndZs)
        {
            List<byte> childPath = new(path.ToArray()) { z };

            if (!leafValuesByPathAndZ.TryGetValue((path, z), out FrE y))
            {
                y = !commByPath.TryGetValue(childPath, out Banderwagon yPoint) ? FrE.Zero : yPoint.MapToScalarField();
            }
            Console.WriteLine($"{path.ToArray().ToHexString()} {z} {y.ToBytes().ToHexString()}");
            ysByPathAndZ.Add((new List<byte>(path.ToArray()), z), y);
        }

        IEnumerable<byte> zs = allPathsAndZs.Select(elem => elem.Item2);
        SortedDictionary<(List<byte>, byte), FrE>.ValueCollection ys = ysByPathAndZ.Values;

        List<VerkleVerifierQuery> queries = new(comms.Length);

        foreach (((FrE y, byte z), Banderwagon comm) in ys.Zip(zs).Zip(comms))
        {
            VerkleVerifierQuery query = new(comm, z, y);
            queries.Add(query);
        }

        Transcript proverTranscript = new("vt");
        MultiProof proofVerifier = new(CRS.Instance, PreComputedWeights.Instance);

        return proofVerifier.CheckMultiProof(proverTranscript, queries.ToArray(), proof);
    }
}
