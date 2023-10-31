// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Blockchain.Synchronization;

namespace Nethermind.Synchronization.FastBlocks;

internal class BodiesSyncStatusList: SyncStatusList
{
    private readonly ISyncConfig _syncConfig;

    public BodiesSyncStatusList(
        IBlockTree blockTree,
        ISyncConfig syncConfig
    ) : base(blockTree)
    {
        _syncConfig = syncConfig;
    }

    public override void Reset()
    {
        base.Reset(
            _syncConfig.PivotNumberParsed,
            _blockTree.LowestInsertedBodyNumber,
            _syncConfig.AncientBodiesBarrier);
    }
}
