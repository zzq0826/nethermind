// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using System.Diagnostics;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Field.Montgomery.FrEElement;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Polynomial;
using Nethermind.Verkle.Proofs;
using Nethermind.Verkle.Utils;
using Nethermind.Verkle.VerkleNodes;

namespace Nethermind.Verkle;

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
        queries.AddRange(OpenStemCommitment(stemProof));

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

    private IEnumerable<VerkleProverQuery> OpenStemCommitment(Dictionary<byte[], HashSet<byte>> stemProof)
    {
        List<VerkleProverQuery> queries = new List<VerkleProverQuery>();

        foreach (KeyValuePair<byte[], HashSet<byte>> proofData in stemProof)
        {
            SuffixTree? suffix = _stateDb.GetStem(proofData.Key);
            queries.AddRange(OpenExtensionCommitment(proofData.Key, proofData.Value, suffix));
            if (proofData.Value.Count == 0) return queries;

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
