// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;

namespace Nethermind.Synchronization.FastBlocks;

internal class ReceiptsSyncStatusList: SyncStatusList
{
    private readonly ISyncConfig _syncConfig;
    private readonly IReceiptStorage _receiptStorage;

    public ReceiptsSyncStatusList(
        IBlockTree blockTree,
        IReceiptStorage receiptStorage,
        ISyncConfig syncConfig
    ) : base(blockTree)
    {
        _syncConfig = syncConfig;
        _receiptStorage = receiptStorage;
    }

    public override void Reset()
    {
        base.Reset(
            _syncConfig.PivotNumberParsed,
            _receiptStorage.LowestInsertedReceiptBlockNumber,
            _syncConfig.AncientReceiptsBarrier);
    }
}
