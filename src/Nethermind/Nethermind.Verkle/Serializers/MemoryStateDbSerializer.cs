// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.VerkleNodes;
using Nethermind.Verkle.VerkleStateDb;

namespace Nethermind.Verkle.Serializers;
using BranchStore = Dictionary<byte[], InternalNode?>;
using LeafStore = Dictionary<byte[], byte[]?>;
using SuffixStore = Dictionary<byte[], SuffixTree?>;


public class MemoryStateDbSerializer : IRlpStreamDecoder<MemoryStateDb>
{
    public static MemoryStateDbSerializer Instance => new MemoryStateDbSerializer();

    public int GetLength(MemoryStateDb item, RlpBehaviors rlpBehaviors)
    {
        int length = 0;
        length += Rlp.LengthOfSequence(LeafStoreSerializer.Instance.GetLength(item.LeafTable, RlpBehaviors.None));
        length += Rlp.LengthOfSequence(SuffixStoreSerializer.Instance.GetLength(item.StemTable, RlpBehaviors.None));
        length += Rlp.LengthOfSequence(BranchStoreSerializer.Instance.GetLength(item.BranchTable, RlpBehaviors.None));
        return length;
    }

    public int GetLength(MemoryStateDb item, RlpBehaviors rlpBehaviors, out int contentLength)
    {
        contentLength = GetLength(item, rlpBehaviors);
        return Rlp.LengthOfSequence(contentLength);
    }

    public MemoryStateDb Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        return new MemoryStateDb(
            LeafStoreSerializer.Instance.Decode(rlpStream),
            SuffixStoreSerializer.Instance.Decode(rlpStream),
            BranchStoreSerializer.Instance.Decode(rlpStream)
        );
    }
    public void Encode(RlpStream stream, MemoryStateDb item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        LeafStoreSerializer.Instance.Encode(stream, item.LeafTable);
        SuffixStoreSerializer.Instance.Encode(stream, item.StemTable);
        BranchStoreSerializer.Instance.Encode(stream, item.BranchTable);
    }
}
