// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Store.Nodes;

public interface ITrieNode
{
    public NodeType NodeType { get; }

}

public enum NodeType : byte
{
    Unknown = 0,

    // merkle tree
    Branch = 1,
    Extension = 2,
    Leaf = 3,

    // //verkle tree
    // BranchNode = 4,
    // StemNode = 5
}
