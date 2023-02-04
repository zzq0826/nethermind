// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using Nethermind.Core.Collections;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Field.Montgomery.FrEElement;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Nodes;
using Nethermind.Verkle.Polynomial;
using Nethermind.Verkle.Proofs;
using Nethermind.Verkle.Utils;

namespace Nethermind.Verkle;

public struct VerkleProof
{
    public VerificationHint VerifyHint;
    public Banderwagon[] CommsSorted;
    public VerkleProofStruct Proof;
}

public struct VerificationHint
{
    public byte[] Depths;
    public ExtPresent[] ExtensionPresent;
    public byte[][] DifferentStemNoProof;
}

public struct UpdateHint
{
    public SortedDictionary<byte[], (ExtPresent, byte)> DepthAndExtByStem;
    public SortedDictionary<List<byte>, Banderwagon> CommByPath;
    public SortedDictionary<List<byte>, byte[]> DifferentStemNoProof;
}

public enum ExtPresent
{
    None,
    DifferentStem,
    Present
}

public struct SuffixPoly
{
    public FrE[] c1;
    public FrE[] c2;
}

public class VerkleProver
{
    private readonly IVerkleStore _stateDb;
    private Dictionary<byte[], FrE[]> _proofBranchPolynomialCache = new Dictionary<byte[], FrE[]>(Bytes.EqualityComparer);
    private Dictionary<byte[], SuffixPoly> _proofStemPolynomialCache = new Dictionary<byte[], SuffixPoly>(Bytes.EqualityComparer);

    public VerkleProver(IDbProvider dbProvider)
    {
        VerkleStateStore stateDb = new VerkleStateStore(dbProvider);
        _stateDb = new CompositeVerkleStateStore(stateDb);
    }

    public VerkleProver(IVerkleStore stateStore)
    {
        _stateDb = new CompositeVerkleStateStore(stateStore);
    }

    public bool VerifyVerkleProof(VerkleProof proof, List<byte[]> keys, List<byte[]?> values, Banderwagon root)
    {
        List<Banderwagon> commSortedByPath = new List<Banderwagon>();
        commSortedByPath.Add(root);
        commSortedByPath.AddRange(proof.CommsSorted);

        SortedSet<byte[]> stems = new SortedSet<byte[]>(keys.Select(x => x[..31]));
        SortedDictionary<byte[], (ExtPresent, byte)> depthsAndExtByStem = new SortedDictionary<byte[], (ExtPresent, byte)>(Bytes.Comparer);
        SortedSet<byte[]> stemsWithExtension = new SortedSet<byte[]>();
        SortedSet<byte[]> otherStemsUsed = new SortedSet<byte[]>();
        SortedSet<List<byte>> allPaths = new SortedSet<List<byte>>();
        SortedSet<(List<byte>, byte)> allPathsAndZs = new SortedSet<(List<byte>, byte)>();
        SortedDictionary<(List<byte>, byte), FrE> leafValuesByPathAndZ = new SortedDictionary<(List<byte>, byte), FrE>();
        SortedDictionary<List<byte>, byte[]> otherStemsByPrefix = new SortedDictionary<List<byte>, byte[]>();


        foreach (((byte[] stem, byte depth) , ExtPresent extPresent)  in stems.Zip(proof.VerifyHint.Depths).Zip(proof.VerifyHint.ExtensionPresent))
        {
            depthsAndExtByStem.Add(stem, (extPresent, depth));

            switch (extPresent)
            {
                case ExtPresent.Present:
                    stemsWithExtension.Add(stem);
                    break;
                case ExtPresent.None:
                case ExtPresent.DifferentStem:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

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

                    leafValuesByPathAndZ.Add((new List<byte>(stem[..depth]), 0), FrE.One);

                    // since the stem was different - value should not have been set
                    if (value != null) return false;

                    Debug.Assert(depth != stem.Length);

                    byte[] otherStem;

                    byte[][] found = stemsWithExtension.Where(x => x[..depth].SequenceEqual(stem[..depth])).ToArray();

                    switch (found.Length)
                    {
                        case 0:
                            found = proof.VerifyHint.DifferentStemNoProof.Where(x => x[..depth].SequenceEqual(stem[..depth])).ToArray();
                            byte[] encounteredStem = found[^1];
                            otherStem = encounteredStem;
                            otherStemsUsed.Add(encounteredStem);

                            // Add extension node to proof in particular, we only want to open at (1, stem)
                            leafValuesByPathAndZ.Add((new List<byte>(stem[..depth]), 1), FrE.FromBytesReduced(encounteredStem.Reverse().ToArray()));
                            break;
                        case 1:
                            otherStem = found[0];
                            break;
                        default:
                            throw new NotSupportedException($"found more than one instance of stem_with_extension at depth {depth}, see: {string.Join(" | ", found.Select(x => string.Join(", ", x)))}");
                    }

                    otherStemsByPrefix.Add(stem[..depth].ToList(), otherStem);
                    break;
                case ExtPresent.Present:
                    allPaths.Add(new List<byte>(stem[..depth]));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 0));
                    allPathsAndZs.Add((new List<byte>(stem[..depth]), 1));

                    leafValuesByPathAndZ.Add((new List<byte>(stem[..depth]), 0), FrE.One);
                    if (extPres == ExtPresent.Present)
                    {
                        byte suffix = key[31];
                        byte openingIndex = suffix < 128 ? (byte) 2 : (byte) 3;

                        allPathsAndZs.Add((new List<byte>(stem[..depth]), openingIndex));
                        leafValuesByPathAndZ.Add((new List<byte>(stem[..depth]), 1), FrE.FromBytesReduced(stem.Reverse().ToArray()));

                        List<byte> suffixTreePath = new List<byte>(stem[..depth]);
                        suffixTreePath.Add(openingIndex);

                        allPaths.Add(suffixTreePath);
                        byte valLowerIndex = (byte)(2 * (suffix % 128));
                        byte valUpperIndex = (byte)(valLowerIndex + 1);

                        allPathsAndZs.Add((suffixTreePath, valLowerIndex));
                        allPathsAndZs.Add((suffixTreePath, valUpperIndex));

                        (FrE valLow, FrE valHigh) = VerkleUtils.BreakValueInLowHigh(value);

                        leafValuesByPathAndZ.Add((suffixTreePath, valLowerIndex), valLow);
                        leafValuesByPathAndZ.Add((suffixTreePath, valUpperIndex), valHigh);
                    }
                    break;
                case ExtPresent.None:
                    // If the extension was not present, then the value should be None
                    if (value != null) return false;

                    if (depth == 1)
                    {
                        leafValuesByPathAndZ.Add((new List<byte>(), stem[depth-1]), FrE.Zero);
                    }
                    else
                    {
                        leafValuesByPathAndZ.Add(
                            (stem[..depth].ToList(), stem[depth-1]), FrE.Zero
                            );
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Debug.Assert(proof.VerifyHint.DifferentStemNoProof.SequenceEqual(otherStemsUsed));
        Debug.Assert(commSortedByPath.Count == allPaths.Count);

        SortedDictionary<List<byte>, Banderwagon> commByPath = new SortedDictionary<List<byte>, Banderwagon>();
        foreach ((List<byte> path, Banderwagon comm) in allPaths.Zip(commSortedByPath))
        {
            commByPath[path] = comm;
        }

        SortedDictionary<(List<byte>, byte), Banderwagon> commByPathAndZ = new SortedDictionary<(List<byte>, byte), Banderwagon>();
        foreach ((List<byte> path, byte z) in allPathsAndZs)
        {
            commByPathAndZ[(path, z)] = commByPath[path];
        }

        SortedDictionary<(List<byte>, byte), FrE> ysByPathAndZ = new SortedDictionary<(List<byte>, byte), FrE>();
        foreach ((List<byte> path, byte z) in allPathsAndZs)
        {
            List<byte> childPath = new List<byte>(path.ToArray())
            {
                z
            };

            FrE y;
            if (!leafValuesByPathAndZ.TryGetValue((path, z), out y))
            {
                y = FrE.FromBytesReduced(commByPath[childPath].MapToField());
            }
            ysByPathAndZ.Add((path, z), y);
        }

        SortedDictionary<(List<byte>, byte), Banderwagon>.ValueCollection cs = commByPathAndZ.Values;

        IEnumerable<FrE> zs = allPathsAndZs.Select(elem => new FrE(elem.Item2));
        SortedDictionary<(List<byte>, byte), FrE>.ValueCollection ys = ysByPathAndZ.Values;

        List<VerkleVerifierQuery> queries = new List<VerkleVerifierQuery>(cs.Count);

        foreach (((FrE y, FrE z) , Banderwagon comm) in ys.Zip(zs).Zip(cs))
        {
            VerkleVerifierQuery query = new VerkleVerifierQuery(comm, z, y);
            queries.Add(query);
        }

        UpdateHint updateHint = new UpdateHint()
        {
            DepthAndExtByStem = depthsAndExtByStem, CommByPath = commByPath, DifferentStemNoProof = otherStemsByPrefix
        };

        Transcript proverTranscript = new Transcript("vt");


        MultiProof proofVerifier = new MultiProof(CRS.Instance, PreComputeWeights.Init());


        return proofVerifier.CheckMultiProof(proverTranscript, queries.ToArray(), proof.Proof);
    }

    public void CreateVerkleProof(List<byte[]> keys)
    {
        _proofBranchPolynomialCache.Clear();
        _proofStemPolynomialCache.Clear();

        // generate prover path for keys
        Dictionary<byte[], HashSet<byte>> stemProof = new Dictionary<byte[], HashSet<byte>>(Bytes.EqualityComparer);
        Dictionary<byte[], HashSet<byte>> branchProof = new Dictionary<byte[], HashSet<byte>>(Bytes.EqualityComparer);

        foreach (byte[] key in keys)
        {
            Debug.Assert(key.Length == 32);
            for (int i = 0; i < 32; i++)
            {
                byte[] currentPath = key[..i];
                InternalNode? node = _stateDb.GetBranch(currentPath);
                if (node != null)
                {
                    switch (node.NodeType)
                    {
                        case NodeType.BranchNode:
                            CreateBranchProofPolynomialIfNotExist(currentPath);
                            branchProof.TryAdd(currentPath, new HashSet<byte>());
                            branchProof[currentPath].Add(key[i]);
                            continue;
                        case NodeType.StemNode:
                            byte[] keyStem = key[..31];
                            CreateStemProofPolynomialIfNotExist(keyStem);
                            stemProof.TryAdd(keyStem, new HashSet<byte>());
                            if (keyStem.SequenceEqual(node.Stem)) stemProof[keyStem].Add(key[31]);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                // reaching here means end of the path for the leaf
                break;
            }
        }

        List<VerkleProverQuery> queries = new List<VerkleProverQuery>();
        queries.AddRange(OpenBranchCommitment(branchProof));
        queries.AddRange(OpenStemCommitment(stemProof, out List<byte[]> stemWithNoProof));

        MultiProof proofConstructor = new MultiProof(CRS.Instance, PreComputeWeights.Init());


        Transcript proverTranscript = new Transcript("vt");
        VerkleProofStruct proof = proofConstructor.MakeMultiProof(proverTranscript, queries);
    }

    private IEnumerable<VerkleProverQuery> OpenBranchCommitment(Dictionary<byte[], HashSet<byte>> branchProof)
    {
        List<VerkleProverQuery> queries = new List<VerkleProverQuery>();
        foreach (KeyValuePair<byte[], HashSet<byte>> proofData in branchProof)
        {
            if(!_proofBranchPolynomialCache.TryGetValue(proofData.Key, out FrE[] poly)) throw new EvaluateException();
            InternalNode? node = _stateDb.GetBranch(proofData.Key);
            queries.AddRange(proofData.Value.Select(childIndex => new VerkleProverQuery(new LagrangeBasis(poly), node!._internalCommitment.Point, childIndex, poly[childIndex])));
        }
        return queries;
    }

    private IEnumerable<VerkleProverQuery> OpenStemCommitment(Dictionary<byte[], HashSet<byte>> stemProof, out List<byte[]> stemWithNoProof)
    {
        stemWithNoProof = new List<byte[]>();
        List<VerkleProverQuery> queries = new List<VerkleProverQuery>();

        foreach (KeyValuePair<byte[], HashSet<byte>> proofData in stemProof)
        {
            SuffixTree? suffix = _stateDb.GetStem(proofData.Key);
            queries.AddRange(OpenExtensionCommitment(proofData.Key, proofData.Value, suffix));
            if (proofData.Value.Count == 0)
            {
                stemWithNoProof.Add(proofData.Key);
                continue;
            }

            _proofStemPolynomialCache.TryGetValue(proofData.Key, out SuffixPoly hashStruct);

            FrE[] c1Hashes = hashStruct.c1;
            FrE[] c2Hashes = hashStruct.c2;

            foreach (byte valueIndex in proofData.Value)
            {
                int valueLowerIndex = 2 * (valueIndex % 128);
                int valueUpperIndex = valueLowerIndex + 1;

                (FrE valueLow, FrE valueHigh) = VerkleUtils.BreakValueInLowHigh(_stateDb.GetLeaf(proofData.Key.Append(valueIndex).ToArray()));

                int offset = valueIndex < 128 ? 0 : 128;

                Banderwagon commitment;
                FrE[] poly;
                switch (offset)
                {
                    case 0:
                        commitment = suffix.C1.Point;
                        poly = c1Hashes.ToArray();
                        break;
                    case 128:
                        commitment = suffix.C2.Point;
                        poly = c2Hashes.ToArray();
                        break;
                    default:
                        throw new Exception("unreachable");
                }

                VerkleProverQuery openAtValLow = new VerkleProverQuery(new LagrangeBasis(poly), commitment, (ulong)valueLowerIndex, valueLow);
                VerkleProverQuery openAtValUpper = new VerkleProverQuery(new LagrangeBasis(poly), commitment, (ulong)valueUpperIndex, valueHigh);

                queries.Add(openAtValLow);
                queries.Add(openAtValUpper);
            }

        }

        return queries;
    }

    private IEnumerable<VerkleProverQuery> OpenExtensionCommitment(byte[] stem, HashSet<byte> value, SuffixTree? suffix)
    {
        List<VerkleProverQuery> queries = new List<VerkleProverQuery>();
        FrE[] extPoly =
        {
            FrE.One, FrE.FromBytesReduced(stem.Reverse().ToArray()), suffix.C1.PointAsField, suffix.C2.PointAsField
        };

        VerkleProverQuery openAtOne = new VerkleProverQuery(new LagrangeBasis(extPoly), suffix.ExtensionCommitment.Point, 0, FrE.One);
        VerkleProverQuery openAtStem = new VerkleProverQuery(new LagrangeBasis(extPoly), suffix.ExtensionCommitment.Point, 1, FrE.FromBytesReduced(stem.Reverse().ToArray()));
        queries.Add(openAtOne);
        queries.Add(openAtStem);

        bool openC1 = false;
        bool openC2 = false;
        foreach (byte valueIndex in value)
        {
            if (valueIndex < 128) openC1 = true;
            else openC2 = true;
        }

        if (openC1)
        {
            VerkleProverQuery openAtC1 = new VerkleProverQuery(new LagrangeBasis(extPoly), suffix.ExtensionCommitment.Point, 2, suffix.C1.PointAsField);
            queries.Add(openAtC1);
        }

        if (openC2)
        {
            VerkleProverQuery openAtC2 = new VerkleProverQuery(new LagrangeBasis(extPoly), suffix.ExtensionCommitment.Point, 3, suffix.C2.PointAsField);
            queries.Add(openAtC2);
        }

        return queries;
    }



    private void CreateBranchProofPolynomialIfNotExist(byte[] path)
    {
        if (_proofBranchPolynomialCache.ContainsKey(path)) return;

        FrE[] newPoly = new FrE[256];
        for (int i = 0; i < 256; i++)
        {
            InternalNode? node = _stateDb.GetBranch(path.Append((byte)i).ToArray());
            newPoly[i] = node == null ? FrE.Zero : node._internalCommitment.PointAsField;
        }
        _proofBranchPolynomialCache[path] = newPoly;
    }

    private void CreateStemProofPolynomialIfNotExist(byte[] stem)
    {
        if (_proofStemPolynomialCache.ContainsKey(stem)) return;

        List<FrE> c1Hashes = new List<FrE>(256);
        List<FrE> c2Hashes = new List<FrE>(256);
        for (int i = 0; i < 128; i++)
        {
            (FrE valueLow, FrE valueHigh) = VerkleUtils.BreakValueInLowHigh(_stateDb.GetLeaf(stem.Append((byte)i).ToArray()));
            c1Hashes.Add(valueLow);
            c1Hashes.Add(valueHigh);
        }

        for (int i = 128; i < 256; i++)
        {
            (FrE valueLow, FrE valueHigh) = VerkleUtils.BreakValueInLowHigh(_stateDb.GetLeaf(stem.Append((byte)i).ToArray()));
            c2Hashes.Add(valueLow);
            c2Hashes.Add(valueHigh);
        }
        _proofStemPolynomialCache[stem] = new SuffixPoly()
        {
            c1 = c1Hashes.ToArray(),
            c2 = c2Hashes.ToArray()
        };
    }

}
