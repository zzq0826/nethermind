// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Serializers;

public class InternalNodeSerializer : IRlpStreamDecoder<InternalNode>, IRlpObjectDecoder<InternalNode>
{
    public static InternalNodeSerializer Instance => new InternalNodeSerializer();
    public int GetLength(InternalNode item, RlpBehaviors rlpBehaviors)
    {
        return item.NodeType switch
        {
            VerkleNodeType.BranchNode => 1 + 32, // NodeType + InternalCommitment
            VerkleNodeType.StemNode => 1 + 31 + 32 + 32 + 32, // NodeType + C1 + C2 + InternalCommitment
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public InternalNode Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        VerkleNodeType nodeType = (VerkleNodeType)rlpStream.ReadByte();
        switch (nodeType)
        {
            case VerkleNodeType.BranchNode:
                InternalNode node = new InternalNode(VerkleNodeType.BranchNode);
                node.UpdateCommitment(new Banderwagon(rlpStream.Read(32).ToArray()));
                return node;
            case VerkleNodeType.StemNode:
                byte[] stem = rlpStream.Read(31).ToArray();
                byte[] c1 = rlpStream.Read(32).ToArray();
                byte[] c2 = rlpStream.Read(32).ToArray();
                byte[] extCommit = rlpStream.Read(32).ToArray();
                return new InternalNode(VerkleNodeType.StemNode, stem, c1, c2, extCommit);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    public void Encode(RlpStream stream, InternalNode item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        switch (item.NodeType)
        {
            case VerkleNodeType.BranchNode:
                stream.WriteByte((byte)VerkleNodeType.BranchNode);
                stream.Write(item.InternalCommitment.Point.ToBytes());
                break;
            case VerkleNodeType.StemNode:
                stream.WriteByte((byte)VerkleNodeType.StemNode);
                stream.Write(item.Stem!);
                stream.Write(item.C1!.Point.ToBytes());
                stream.Write(item.C2!.Point.ToBytes());
                stream.Write(item.InternalCommitment.Point.ToBytes());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    public Rlp Encode(InternalNode item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int length = GetLength(item, rlpBehaviors);
        RlpStream stream = new RlpStream(Rlp.LengthOfSequence(length));
        stream.StartSequence(length);
        Encode(stream, item, rlpBehaviors);
        return new Rlp(stream.Data);
    }
    public InternalNode Decode(byte[] data, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        RlpStream stream = data.AsRlpStream();
        stream.ReadSequenceLength();
        return Decode(stream, rlpBehaviors);
    }

}
