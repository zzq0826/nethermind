// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Fields.FrEElement;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Nodes;

public class StemNode : InternalNode
{
    public StemNode(byte[] stem, Commitment suffixCommitment) : base(NodeType.StemNode, stem, suffixCommitment)
    {
    }
}

public class BranchNode : InternalNode
{
    public BranchNode() : base(NodeType.BranchNode)
    {
    }
}

public class InternalNode
{
    public bool IsStem => _nodeType == NodeType.StemNode;
    public bool IsBranchNode => _nodeType == NodeType.BranchNode;

    public readonly Commitment _internalCommitment;

    public readonly NodeType _nodeType;

    private byte[]? _stem;
    public byte[] Stem
    {
        get
        {
            Debug.Assert(_stem != null, nameof(_stem) + " != null");
            return _stem;
        }
    }

    protected InternalNode(NodeType nodeType, byte[] stem, Commitment suffixCommitment)
    {
        switch (nodeType)
        {
            case NodeType.StemNode:
                _nodeType = NodeType.StemNode;
                _stem = stem;
                _internalCommitment = suffixCommitment;
                break;
            case NodeType.BranchNode:
            default:
                throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null);
        }
    }

    protected InternalNode(NodeType nodeType)
    {
        _nodeType = nodeType;
        _internalCommitment = new Commitment();
    }

    public FrE UpdateCommitment(Banderwagon point)
    {
        FrE prevCommit = _internalCommitment.PointAsField.Dup();
        _internalCommitment.AddPoint(point);
        return _internalCommitment.PointAsField - prevCommit;
    }
}
