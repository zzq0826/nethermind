// Copyright 2022 Demerzel Solutions Limited
// Licensed under Apache-2.0. For full terms, see LICENSE in the project root.

using Nethermind.Db;
using Nethermind.Verkle.VerkleNodes;

namespace Nethermind.Verkle.VerkleStateDb;

public interface IVerkleDb
{
    bool GetLeaf(byte[] key, out byte[]? value);
    bool GetStem(byte[] key, out SuffixTree? value);
    bool GetBranch(byte[] key, out InternalNode? value);
    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetStem(byte[] stemKey, SuffixTree suffixTree);
    void SetBranch(byte[] branchKey, InternalNode internalNodeValue);
    void RemoveLeaf(byte[] leafKey);
    void RemoveStem(byte[] stemKey);
    void RemoveBranch(byte[] branchKey);

    void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf);
    void BatchStemInsert(IEnumerable<KeyValuePair<byte[], SuffixTree?>> suffixLeaf);
    void BatchBranchInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> branchLeaf);
}

public interface IVerkleMemoryDb : IVerkleDb
{
    byte[] Encode();
    public Dictionary<byte[], byte[]?> LeafTable { get; }
    public Dictionary<byte[], SuffixTree?> StemTable { get; }
    public Dictionary<byte[], InternalNode?> BranchTable { get; }
}

public interface IVerkleKeyValueDb : IVerkleDb
{
    public IDb LeafDb { get; }
    public IDb StemDb { get; }
    public IDb BranchDb { get; }
}
