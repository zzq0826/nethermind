// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Store.Nodes.Patricia;

public struct ExtensionNode: IMerkleNode
{
    public NodeType NodeType => NodeType.Extension;
    public byte[]? FullRlp { get; set; }
    public Keccak? Keccak { get; set; }
    public byte[] Key { get; set; }
    public byte[] Value { get; set; }

    public void WriteChildRlp(RlpStream destination)
    {
        destination.Write(Array.Empty<byte>());
    }

    public int GetChildRlpLength()
    {
        return 0;
    }
    public IMerkleNode Clone()
    {
        return (ExtensionNode)MemberwiseClone();
    }

    public byte[] Path { get; set; }
}
