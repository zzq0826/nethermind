// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Proofs;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Proofs;

public class WitnessVerkleProof
{
    public Stem[] OtherStems;
    public ExtPresent[] DepthExtensionPresent;
    public Banderwagon[] CommitmentsByPath;
    public Banderwagon D;
    public IpaProofStruct IpaProof;

    public static implicit operator WitnessVerkleProof(VerkleProof proof)
    {
        Stem[] otherStems = new Stem[proof.VerifyHint.DifferentStemNoProof.Length];
        for (int i = 0; i < otherStems.Length; i++)
        {
            otherStems[i] = new Stem(proof.VerifyHint.DifferentStemNoProof[i]);
        }

        return new WitnessVerkleProof()
        {
            OtherStems = otherStems,
            CommitmentsByPath = proof.CommsSorted,
            DepthExtensionPresent = proof.VerifyHint.ExtensionPresent,
            D = proof.Proof.D,
            IpaProof = proof.Proof.IpaProof
        };
    }
}

public struct ExecutionWitness
{
    public StateDiff StateDiff { get; set; }
    public WitnessVerkleProof Proof { get; set; }
}

public struct StateDiff
{
    // max length = 2**16
    public List<StemStateDiff> SuffixDiffs { get; set; }
}

public struct StemStateDiff
{
    // byte31
    public Stem Stem { get; set; }

    // max length = 256
    public List<SuffixStateDiff> SuffixDiffs { get; set; }
}

public struct SuffixStateDiff
{
    public byte Suffix { get; set; }

    // byte32
    public byte[]? CurrentValue { get; set; }

    // byte32
    public byte[]? NewValue { get; set; }
}
