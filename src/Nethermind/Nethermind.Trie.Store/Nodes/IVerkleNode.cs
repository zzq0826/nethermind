// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Trie.Store.Nodes;

public interface IVerkleNode: ITrieNode
{
    public NodeType nodeType { get; }
}
