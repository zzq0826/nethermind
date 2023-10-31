// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Synchronization.FastBlocks;

public interface ISyncStatusList
{
    long LowestInsertWithoutGaps { get; }
    long QueueSize { get; }

    void GetInfosForBatch(BlockInfo[] infos);
    void MarkPending(BlockInfo batchInfo);
    void MarkInserted(long blockNumber);
}
