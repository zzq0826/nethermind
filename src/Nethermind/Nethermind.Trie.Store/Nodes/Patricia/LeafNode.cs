// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Store.Nodes.Patricia;

public struct LeafNode: IMerkleNode
{
    public LeafNode(byte[] key)
    {
        Key = key;
    }
    public NodeType NodeType => NodeType.Leaf;
    public byte[]? FullRlp { get; set; }
    public Keccak? Keccak { get; set; }
    public byte[] Key { get; set; }
    public byte[] Value { get; set; }
    public byte[] Path { get; set; }
    public void WriteChildRlp(RlpStream destination)
    {
        return;
    }
    public int GetChildRlpLength()
    {
        return 0;
    }
    public IMerkleNode Clone()
    {
        return (LeafNode)MemberwiseClone();
    }
}
