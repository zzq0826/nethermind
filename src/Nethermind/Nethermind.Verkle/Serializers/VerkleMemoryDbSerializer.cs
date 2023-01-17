// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.VerkleDb;
using Nethermind.Verkle.VerkleNodes;

namespace Nethermind.Verkle.Serializers;
using BranchStore = ConcurrentDictionary<byte[], InternalNode?>;
using LeafStore = ConcurrentDictionary<byte[], byte[]?>;
using StemStore = ConcurrentDictionary<byte[], SuffixTree?>;

public class VerkleMemoryDbSerializer: IRlpStreamDecoder<VerkleMemoryDb>
{
    public static VerkleMemoryDbSerializer Instance => new VerkleMemoryDbSerializer();

    public int GetLength(VerkleMemoryDb item, RlpBehaviors rlpBehaviors)
    {
        int length = 0;
        length += LeafStoreSerializer.Instance.GetLength(item.LeafTable, RlpBehaviors.None);
        length += StemStoreSerializer.Instance.GetLength(item.StemTable, RlpBehaviors.None);
        length += BranchStoreSerializer.Instance.GetLength(item.BranchTable, RlpBehaviors.None);
        return length;
    }

    public int GetLength(VerkleMemoryDb item, RlpBehaviors rlpBehaviors, out int contentLength)
    {
        contentLength = GetLength(item, rlpBehaviors);
        return Rlp.LengthOfSequence(contentLength);
    }

    public VerkleMemoryDb Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        return new VerkleMemoryDb(
            LeafStoreSerializer.Instance.Decode(rlpStream),
            StemStoreSerializer.Instance.Decode(rlpStream),
            BranchStoreSerializer.Instance.Decode(rlpStream)
        );
    }
    public void Encode(RlpStream stream, VerkleMemoryDb item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        LeafStoreSerializer.Instance.Encode(stream, item.LeafTable);
        StemStoreSerializer.Instance.Encode(stream, item.StemTable);
        BranchStoreSerializer.Instance.Encode(stream, item.BranchTable);
    }
}
