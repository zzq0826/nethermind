// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Trie.Store.Nodes.Patricia;

public class LeafNode: IMerkleNode
{

    public TreeNodeType NodeType => TreeNodeType.Leaf;
    public byte[]? FullRlp { get; set; }
    public Keccak? Keccak { get; set; }
    public byte[] Key { get; }
    public byte[] Value { get; }
}
