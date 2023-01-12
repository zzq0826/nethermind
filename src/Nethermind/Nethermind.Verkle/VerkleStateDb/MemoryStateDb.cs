// Copyright 2022 Demerzel Solutions Limited
// Licensed under Apache-2.0. For full terms, see LICENSE in the project root.

using Nethermind.Core.Extensions;
using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Serializers;
using Nethermind.Verkle.VerkleNodes;

namespace Nethermind.Verkle.VerkleStateDb;
using BranchStore = Dictionary<byte[], InternalNode?>;
using LeafStore = Dictionary<byte[], byte[]?>;
using SuffixStore = Dictionary<byte[], SuffixTree?>;

public class MemoryStateDb : IVerkleMemoryDb
{
    public Dictionary<byte[], byte[]?> LeafTable { get; }
    public Dictionary<byte[], SuffixTree?> StemTable { get; }
    public Dictionary<byte[], InternalNode?> BranchTable { get; }

    public MemoryStateDb()
    {
        LeafTable = new Dictionary<byte[], byte[]?>(Bytes.EqualityComparer);
        StemTable = new Dictionary<byte[], SuffixTree?>(Bytes.EqualityComparer);
        BranchTable = new Dictionary<byte[], InternalNode?>(Bytes.EqualityComparer);
    }

    public MemoryStateDb(LeafStore leafTable, SuffixStore stemTable, BranchStore branchTable)
    {
        LeafTable = leafTable;
        StemTable = stemTable;
        BranchTable = branchTable;
    }

    public byte[] Encode()
    {
        int contentLength = MemoryStateDbSerializer.Instance.GetLength(this, RlpBehaviors.None);
        RlpStream stream = new RlpStream(Rlp.LengthOfSequence(contentLength));
        stream.StartSequence(contentLength);
        MemoryStateDbSerializer.Instance.Encode(stream, this);
        return stream.Data ?? throw new ArgumentException();
    }

    public static MemoryStateDb Decode(byte[] data)
    {
        RlpStream stream = data.AsRlpStream();
        stream.ReadSequenceLength();
        return MemoryStateDbSerializer.Instance.Decode(stream);
    }

    public bool GetLeaf(byte[] key, out byte[]? value) => LeafTable.TryGetValue(key, out value);
    public bool GetStem(byte[] key, out SuffixTree? value) => StemTable.TryGetValue(key, out value);
    public bool GetBranch(byte[] key, out InternalNode? value) => BranchTable.TryGetValue(key, out value);
    public void SetLeaf(byte[] leafKey, byte[]? leafValue) => LeafTable[leafKey] = leafValue;
    public void SetStem(byte[] stemKey, SuffixTree? suffixTree) => StemTable[stemKey] = suffixTree;
    public void SetBranch(byte[] branchKey, InternalNode? internalNodeValue) => BranchTable[branchKey] = internalNodeValue;
    public void RemoveLeaf(byte[] leafKey)
    {
        LeafTable.Remove(leafKey);
    }
    public void RemoveStem(byte[] stemKey)
    {
        StemTable.Remove(stemKey);
    }
    public void RemoveBranch(byte[] branchKey)
    {
        BranchTable.Remove(branchKey);
    }

    public void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf)
    {
        foreach ((byte[] key, byte[]? value) in keyLeaf)
        {
            SetLeaf(key, value);
        }
    }
    public void BatchStemInsert(IEnumerable<KeyValuePair<byte[], SuffixTree?>> suffixLeaf)
    {
        foreach ((byte[] key, SuffixTree? value) in suffixLeaf)
        {
            SetStem(key, value);
        }
    }
    public void BatchBranchInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> branchLeaf)
    {
        foreach ((byte[] key, InternalNode? value) in branchLeaf)
        {
            SetBranch(key, value);
        }
    }
}

