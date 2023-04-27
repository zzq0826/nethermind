// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Tree.Nodes;

namespace Nethermind.Verkle.Tree.Serializers;

/// <summary>
/// Serialize the suffix node to store in a key value store
/// stem (31 bytes) + C1.Point (32 Bytes) + C2.Point (32 Bytes) + ExtensionCommitment.Point (32 Bytes)
/// </summary>
public class SuffixTreeSerializer : IRlpStreamDecoder<SuffixTree>
{
    public static SuffixTreeSerializer Instance => new SuffixTreeSerializer();

    public int GetLength(SuffixTree item, RlpBehaviors rlpBehaviors)
    {
        return 31 + 32 + 32 + 32;
    }

    public SuffixTree Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        byte[] stem = rlpStream.Read(31).ToArray();
        byte[] c1 = rlpStream.Read(32).ToArray();
        byte[] c2 = rlpStream.Read(32).ToArray();
        byte[] extCommit = rlpStream.Read(32).ToArray();
        return new SuffixTree(stem, c1, c2, extCommit);
    }

    public void Encode(RlpStream stream, SuffixTree item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.Write(item.Stem);
        stream.Write(item.C1.Point.ToBytes());
        stream.Write(item.C2.Point.ToBytes());
        stream.Write(item.ExtensionCommitment.Point.ToBytes());
    }

    public byte[] Encode(SuffixTree item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int length = GetLength(item, rlpBehaviors);
        RlpStream stream = new RlpStream(Rlp.LengthOfSequence(length));
        stream.StartSequence(length);
        Encode(stream, item, rlpBehaviors);
        return stream.Data!;
    }

    public SuffixTree Decode(byte[] data, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        RlpStream stream = data.AsRlpStream();
        stream.ReadSequenceLength();
        return Decode(stream, rlpBehaviors);
    }
}
