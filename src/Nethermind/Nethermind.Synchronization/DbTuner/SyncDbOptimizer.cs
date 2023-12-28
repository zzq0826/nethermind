// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Synchronization;
using Nethermind.Db;
using Nethermind.Synchronization.FastBlocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.SnapSync;

namespace Nethermind.Synchronization.DbTuner;

public class SyncDbTuner
{
    private readonly IDbMeta? _stateDb;
    private readonly IDbMeta? _codeDb;
    private readonly IDbMeta? _blockDb;
    private readonly IDbMeta? _receiptDb;

    private readonly IDbMeta.TuneType _tuneType;
    private readonly IDbMeta.TuneType _blocksDbTuneType;

    public SyncDbTuner(
        ISyncConfig syncConfig,
        ISyncFeed<SnapSyncBatch>? snapSyncFeed,
        ISyncFeed<BodiesSyncBatch>? bodiesSyncFeed,
        ISyncFeed<ReceiptsSyncBatch>? receiptSyncFeed,
        IDbMeta? stateDb,
        IDbMeta? codeDb,
        IDbMeta? blockDb,
        IDbMeta? receiptDb
    )
    {
        // Only these three make sense as they are write heavy
        // Headers is used everywhere, so slowing read might slow the whole sync.
        // Statesync is read heavy, Forward sync is just plain too slow to saturate IO.
        if (snapSyncFeed != null)
        {
            snapSyncFeed.StateChanged += SnapStateChanged;
        }

        if (bodiesSyncFeed != null)
        {
            bodiesSyncFeed.StateChanged += BodiesStateChanged;
        }

        if (receiptSyncFeed != null)
        {
            receiptSyncFeed.StateChanged += ReceiptsStateChanged;
        }

        _stateDb = stateDb;
        _codeDb = codeDb;
        _blockDb = blockDb;
        _receiptDb = receiptDb;

        _tuneType = syncConfig.TuneDbMode;
        _blocksDbTuneType = syncConfig.BlocksDbTuneDbMode;
    }

    private void SnapStateChanged(object? sender, SyncFeedStateEventArgs e)
    {
        if (e.NewState == SyncFeedState.Active)
        {
            _stateDb?.Tune(_tuneType);
            _codeDb?.Tune(_tuneType);
        }
        else if (e.NewState == SyncFeedState.Finished)
        {
            _stateDb?.Tune(IDbMeta.TuneType.Default);
            _codeDb?.Tune(IDbMeta.TuneType.Default);
        }
    }

    private void BodiesStateChanged(object? sender, SyncFeedStateEventArgs e)
    {
        if (e.NewState == SyncFeedState.Active)
        {
            _blockDb?.Tune(_blocksDbTuneType);
        }
        else if (e.NewState == SyncFeedState.Finished)
        {
            _blockDb?.Tune(IDbMeta.TuneType.Default);
        }
    }

    private void ReceiptsStateChanged(object? sender, SyncFeedStateEventArgs e)
    {
        if (e.NewState == SyncFeedState.Active)
        {
            _receiptDb?.Tune(_tuneType);
        }
        else if (e.NewState == SyncFeedState.Finished)
        {
            _receiptDb?.Tune(IDbMeta.TuneType.Default);
        }
    }
}
