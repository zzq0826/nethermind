// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Proofs;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Proofs;


public struct ExecutionWitness
{
    public StateDiff StateDiff { get; set; }
    public WitnessVerkleProof Proof { get; set; }
}

public class WitnessVerkleProof
{
    public Stem[] OtherStems { get; set; }
    public ExtPresent[] DepthExtensionPresent { get; set; }
    public Banderwagon[] CommitmentsByPath { get; set; }
    public Banderwagon D { get; set; }
    public IpaProofStruct IpaProof { get; set; }

    public WitnessVerkleProof(
        Stem[] otherStems,
        ExtPresent[] depthExtensionPresent,
        Banderwagon[] commitmentsByPath,
        Banderwagon d,
        IpaProofStruct ipaProof)
    {
        OtherStems = otherStems;
        DepthExtensionPresent = depthExtensionPresent;
        CommitmentsByPath = commitmentsByPath;
        D = d;
        IpaProof = ipaProof;
    }

    public static implicit operator WitnessVerkleProof(VerkleProof proof)
    {
        Stem[] otherStems = proof.VerifyHint.DifferentStemNoProof.Select(x => new Stem(x)).ToArray();

        return new WitnessVerkleProof(otherStems,
            proof.VerifyHint.ExtensionPresent,
            proof.CommsSorted,
            proof.Proof.D,
            proof.Proof.IpaProof
        );
    }
}


public struct StateDiff
{
    public List<StemStateDiff> SuffixDiffs { get; set; }
}

public struct StemStateDiff
{
    public Stem Stem { get; set; }
    public List<SuffixStateDiff> SuffixDiffs { get; set; }
}

public struct SuffixStateDiff
{
    public byte Suffix { get; set; }
    public byte[]? CurrentValue { get; set; }
    public byte[]? NewValue { get; set; }
}
