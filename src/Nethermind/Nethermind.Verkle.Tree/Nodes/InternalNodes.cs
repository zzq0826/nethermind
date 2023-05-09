// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Nodes;

public class InternalNode
{
    public bool IsStem => NodeType == VerkleNodeType.StemNode;
    public bool IsBranchNode => NodeType == VerkleNodeType.BranchNode;
    public VerkleNodeType NodeType { get; }
    public Commitment InternalCommitment { get; }


    /// <summary>
    ///  C1, C2, InitCommitmentHash - only relevant for stem nodes
    /// </summary>
    public Commitment? C1 { get; }
    public Commitment? C2 { get; }
    public FrE? InitCommitmentHash { get; }

    private static readonly Banderwagon _initFirstElementCommitment = Committer.ScalarMul(FrE.One, 0);

    public byte[]? Stem { get; }

    public InternalNode Clone()
    {
        return NodeType switch
        {
            VerkleNodeType.BranchNode => new InternalNode(VerkleNodeType.BranchNode, InternalCommitment.Dup()),
            VerkleNodeType.StemNode => new InternalNode(VerkleNodeType.StemNode, (byte[])Stem!.Clone(), C1!.Dup(), C2!.Dup(), InternalCommitment.Dup(), InitCommitmentHash!.Value.Dup()),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public InternalNode(VerkleNodeType nodeType)
    {
        NodeType = nodeType;
        InternalCommitment = new Commitment();
    }

    public InternalNode(VerkleNodeType nodeType, Commitment commitment)
    {
        NodeType = nodeType;
        InternalCommitment = commitment;
    }

    public InternalNode(VerkleNodeType nodeType, byte[] stem)
    {
        NodeType = nodeType;
        Stem = stem;
        C1 = new Commitment();
        C2 = new Commitment();
        InternalCommitment = new Commitment();
        Banderwagon stemCommitment = GetInitialCommitment();
        InternalCommitment.AddPoint(stemCommitment);
        InitCommitmentHash = InternalCommitment.PointAsField.Dup();
    }

    private InternalNode(VerkleNodeType nodeType, byte[] stem, Commitment c1, Commitment c2, Commitment internalCommitment, FrE initCommitment)
    {
        NodeType = nodeType;
        Stem = stem;
        C1 = c1;
        C2 = c2;
        InternalCommitment = internalCommitment;
        InitCommitmentHash = initCommitment;
    }

    internal InternalNode(VerkleNodeType nodeType, byte[] stem, byte[] c1, byte[] c2, byte[] extCommit)
    {
        NodeType = nodeType;
        Stem = stem;
        C1 = new Commitment(new Banderwagon(c1));
        C2 = new Commitment(new Banderwagon(c2));
        InternalCommitment = new Commitment(new Banderwagon(extCommit));
        InitCommitmentHash = FrE.Zero;

    }

    private Banderwagon GetInitialCommitment()
    {
        return _initFirstElementCommitment +
               Committer.ScalarMul(FrE.FromBytesReduced(Stem!.Reverse().ToArray()), 1);
    }

    public FrE UpdateCommitment(Banderwagon point)
    {
        Debug.Assert(NodeType == VerkleNodeType.BranchNode);
        FrE prevCommit = InternalCommitment.PointAsField.Dup();
        InternalCommitment.AddPoint(point);
        return InternalCommitment.PointAsField - prevCommit;
    }

    public FrE UpdateCommitment(LeafUpdateDelta deltaLeafCommitment)
    {
        Debug.Assert(NodeType == VerkleNodeType.StemNode);
        FrE deltaC1Commit = FrE.Zero;
        FrE deltaC2Commit = FrE.Zero;

        if (deltaLeafCommitment.DeltaC1 is not null)
        {
            FrE oldC1Value = C1!.PointAsField.Dup();
            C1.AddPoint(deltaLeafCommitment.DeltaC1.Value);
            deltaC1Commit = C1.PointAsField - oldC1Value;
        }
        if (deltaLeafCommitment.DeltaC2 is not null)
        {
            FrE oldC2Value = C2!.PointAsField.Dup();
            C2.AddPoint(deltaLeafCommitment.DeltaC2.Value);
            deltaC2Commit = C2.PointAsField - oldC2Value;
        }

        Banderwagon deltaCommit = Committer.ScalarMul(deltaC1Commit, 2)
                                  + Committer.ScalarMul(deltaC2Commit, 3);

        return InternalCommitment.UpdateCommitmentGetDelta(deltaCommit);
    }
}
