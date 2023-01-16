// Copyright 2022 Demerzel Solutions Limited
// Licensed under Apache-2.0. For full terms, see LICENSE in the project root.

namespace Nethermind.Verkle.VerkleStateDb;

public interface IDiffLayer
{
    public void InsertDiff(long blockNumber, IVerkleMemoryDb memory);

    public IVerkleMemoryDb FetchDiff(long blockNumber);

    public IVerkleMemoryDb MergeDiffs(long fromBlock, long toBlock);
}
