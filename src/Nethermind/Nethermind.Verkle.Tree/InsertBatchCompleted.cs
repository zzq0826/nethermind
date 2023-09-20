// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree;

public class InsertBatchCompleted : EventArgs
{
    public InsertBatchCompleted(long blockNumber, VerkleMemoryDb reverseDiff)
    {
        BlockNumber = blockNumber;
        ReverseDiff = reverseDiff;
    }

    public VerkleMemoryDb ReverseDiff { get; }
    public long BlockNumber { get; }
}
