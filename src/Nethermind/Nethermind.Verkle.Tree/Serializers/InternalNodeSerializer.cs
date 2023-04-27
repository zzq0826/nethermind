// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Nodes;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Serializers;

/// <summary>
/// This serializer allows to serialize the internal nodes in verkle trees
/// There are two internal nodes
/// 1. Branch Node
///     just the commitment is needed to be encoded - 32 Bytes
/// 2. Stem Node
///     both the commitment (32 Bytes) and the stem (31 bytes) needs to be encoded - 63 bytes
///
/// Then one more byte to differentiate between two node types
/// </summary>
public class InternalNodeSerializer : IRlpStreamDecoder<InternalNode>
{
    public static InternalNodeSerializer Instance => new InternalNodeSerializer();

    public int GetLength(InternalNode item, RlpBehaviors rlpBehaviors)
    {
        return item._nodeType switch
        {
            NodeType.BranchNode => 1 + 32,  // node type + commitment
            NodeType.StemNode => 1 + 31 + 32,  // node type + stem + commitment
            _ => throw new InvalidDataException($"Type of the internal node: {item} is unknown - {item._nodeType}")
        };
    }

    public InternalNode Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        NodeType nodeType = (NodeType)rlpStream.ReadByte();
        switch (nodeType)
        {
            case NodeType.BranchNode:
                BranchNode node = new BranchNode();
                node.UpdateCommitment(new Banderwagon(rlpStream.Read(32).ToArray()));
                return node;
            case NodeType.StemNode:
                return new StemNode(rlpStream.Read(31).ToArray(), new Commitment(new Banderwagon(rlpStream.Read(32).ToArray())));
            default:
                throw new InvalidDataException($"Type of the internal node is unknown - {nodeType}");
        }
    }

    public void Encode(RlpStream stream, InternalNode item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        switch (item._nodeType)
        {
            case NodeType.BranchNode:
                stream.WriteByte((byte)NodeType.BranchNode);
                stream.Write(item._internalCommitment.Point.ToBytes());
                break;
            case NodeType.StemNode:
                stream.WriteByte((byte)NodeType.StemNode);
                stream.Write(item.Stem);
                stream.Write(item._internalCommitment.Point.ToBytes());
                break;
            default:
                throw new InvalidDataException($"Type of the internal node: {item} is unknown - {item._nodeType}");
        }
    }

    public byte[] Encode(InternalNode item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int length = GetLength(item, rlpBehaviors);
        RlpStream stream = new RlpStream(Rlp.LengthOfSequence(length));
        stream.StartSequence(length);
        Encode(stream, item, rlpBehaviors);
        return stream.Data!;
    }

    public InternalNode Decode(byte[] data, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        RlpStream stream = data.AsRlpStream();
        stream.ReadSequenceLength();
        return Decode(stream, rlpBehaviors);
    }

}
