// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Utils;
using LeafUpdateDelta = Nethermind.Verkle.Tree.Utils.LeafUpdateDelta;

namespace Nethermind.Verkle.Tree.Nodes;

public class SuffixTree
{
    public byte[] Stem { get; }
    public Commitment C1 { get; }
    public Commitment C2 { get; }
    public Commitment ExtensionCommitment { get; }
    public FrE InitCommitmentHash { get; }

    public SuffixTree(byte[] stem)
    {
        Stem = stem;
        C1 = new Commitment();
        C2 = new Commitment();
        ExtensionCommitment = new Commitment();
        Banderwagon stemCommitment = GetInitialCommitment();
        ExtensionCommitment.AddPoint(stemCommitment);
        InitCommitmentHash = ExtensionCommitment.PointAsField.Dup();
    }

    internal SuffixTree(byte[] stem, byte[] c1, byte[] c2, byte[] extCommit)
    {
        Stem = stem;
        C1 = new Commitment(new Banderwagon(c1));
        C2 = new Commitment(new Banderwagon(c2));
        ExtensionCommitment = new Commitment(new Banderwagon(extCommit));
        InitCommitmentHash = FrE.Zero;
    }

    private Banderwagon GetInitialCommitment()
    {
        return Committer.ScalarMul(FrE.One, 0) +
               Committer.ScalarMul(FrE.FromBytesReduced(Stem.Reverse().ToArray()), 1);
    }

    public FrE UpdateCommitment(LeafUpdateDelta deltaLeafCommitment)
    {
        FrE deltaC1Commit = FrE.Zero;
        FrE deltaC2Commit = FrE.Zero;

        if (deltaLeafCommitment.DeltaC1 is not null)
        {
            FrE oldC1Value = C1.PointAsField.Dup();
            C1.AddPoint(deltaLeafCommitment.DeltaC1);
            deltaC1Commit = C1.PointAsField - oldC1Value;
        }
        if (deltaLeafCommitment.DeltaC2 is not null)
        {
            FrE oldC2Value = C2.PointAsField.Dup();
            C2.AddPoint(deltaLeafCommitment.DeltaC2);
            deltaC2Commit = C2.PointAsField - oldC2Value;
        }

        Banderwagon deltaCommit = Committer.ScalarMul(deltaC1Commit, 2)
                                  + Committer.ScalarMul(deltaC2Commit, 3);

        return ExtensionCommitment.UpdateCommitmentGetDelta(deltaCommit);
    }
}
