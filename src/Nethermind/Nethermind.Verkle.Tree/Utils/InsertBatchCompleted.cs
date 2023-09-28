// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.Utils;

public class InsertBatchCompleted : EventArgs
{
    public InsertBatchCompleted(long blockNumber,  ReadOnlyVerkleMemoryDb forwardDiff, VerkleMemoryDb reverseDiff)
    {
        BlockNumber = blockNumber;
        ReverseDiff = reverseDiff;
        ForwardDiff = forwardDiff;
    }

    public VerkleMemoryDb ReverseDiff { get; }
    public  ReadOnlyVerkleMemoryDb ForwardDiff { get; }
    public long BlockNumber { get; }
}
