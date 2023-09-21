// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Verkle.Tree.VerkleDb;

public class ChangeSet
{
    public long BlockNumber { get; }
    public VerkleMemoryDb DiffLayer { get; }

    public ChangeSet(long blockNumber, VerkleMemoryDb diffLayer)
    {
        BlockNumber = blockNumber;
        DiffLayer = diffLayer;
    }
}

public class BatchChangeSet
{
    public long FromBlockNumber { get; }
    public long ToBlockNumber { get; }
    public VerkleMemoryDb DiffLayer { get; }

    public BatchChangeSet(long fromBlockNumber, long toBlockNumber, VerkleMemoryDb diffLayer)
    {
        FromBlockNumber = fromBlockNumber;
        ToBlockNumber = toBlockNumber;
        DiffLayer = diffLayer;
    }
}

