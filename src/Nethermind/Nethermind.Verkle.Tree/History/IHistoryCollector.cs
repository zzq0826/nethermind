// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.History;

public interface IHistoryCollector
{
    void InsertDiff(long blockNumber, VerkleMemoryDb postState, VerkleMemoryDb preState);

    void InsertDiff(long blockNumber, ReadOnlyVerkleMemoryDb postState, VerkleMemoryDb preState);
}
