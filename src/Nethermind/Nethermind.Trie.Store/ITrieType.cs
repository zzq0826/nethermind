// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Trie.Store;

public enum TrieType: byte
{
    Merkle = 0,
    Verkle = 1
}
