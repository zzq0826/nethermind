// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.TrieNodes;

namespace Nethermind.Verkle.Tree.History;

public interface  IHistoryProvider
{
    byte[]? GetLeaf(ReadOnlySpan<byte> key);
    InternalNode? GetInternalNode(ReadOnlySpan<byte> key);
}
