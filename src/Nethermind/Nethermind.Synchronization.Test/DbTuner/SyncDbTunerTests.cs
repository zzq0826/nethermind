// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Synchronization;
using Nethermind.Db;
using Nethermind.Synchronization.DbTuner;
using Nethermind.Synchronization.FastBlocks;
using Nethermind.Synchronization.ParallelSync;
using Nethermind.Synchronization.SnapSync;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Synchronization.Test.DbTuner;

public class SyncDbTunerTests
{
    private IDbMeta.TuneType _tuneType = IDbMeta.TuneType.HeavyWrite;
    private readonly IDbMeta.TuneType _blocksTuneType = IDbMeta.TuneType.AggressiveHeavyWrite;
    private SyncConfig _syncConfig = null!;
    private ISyncFeed<SnapSyncBatch> _snapSyncFeed = null!;
    private ISyncFeed<BodiesSyncBatch> _bodiesSyncFeed = null!;
    private ISyncFeed<ReceiptsSyncBatch> _receiptSyncFeed = null!;
    private IDbMeta _stateDb = null!;
    private IDbMeta _codeDb = null!;
    private IDbMeta _blockDb = null!;
    private IDbMeta _receiptDb = null!;

    [SetUp]
    public void Setup()
    {
        _tuneType = IDbMeta.TuneType.HeavyWrite;
        _syncConfig = new SyncConfig()
        {
            TuneDbMode = _tuneType,
            BlocksDbTuneDbMode = _blocksTuneType,
        };
        _snapSyncFeed = Substitute.For<ISyncFeed<SnapSyncBatch>>();
        _bodiesSyncFeed = Substitute.For<ISyncFeed<BodiesSyncBatch>>();
        _receiptSyncFeed = Substitute.For<ISyncFeed<ReceiptsSyncBatch>>();
        _stateDb = Substitute.For<IDbMeta>();
        _codeDb = Substitute.For<IDbMeta>();
        _blockDb = Substitute.For<IDbMeta>();
        _receiptDb = Substitute.For<IDbMeta>();

        SyncDbTuner _ = new SyncDbTuner(
            _syncConfig,
            _snapSyncFeed,
            _bodiesSyncFeed,
            _receiptSyncFeed,
            _stateDb,
            _codeDb,
            _blockDb,
            _receiptDb);
    }

    [Test]
    public void WhenSnapIsOn_TriggerStateDbTune()
    {
        TestFeedAndDbTune(_snapSyncFeed, _stateDb);
    }

    [Test]
    public void WhenSnapIsOn_TriggerCodeDbTune()
    {
        TestFeedAndDbTune(_snapSyncFeed, _codeDb);
    }

    [Test]
    public void WhenBodiesIsOn_TriggerBlocksDbTune()
    {
        TestFeedAndDbTune(_bodiesSyncFeed, _blockDb, _blocksTuneType);
    }

    [Test]
    public void WhenReceiptsIsOn_TriggerReceiptsDbTune()
    {
        TestFeedAndDbTune(_receiptSyncFeed, _receiptDb);
    }

    private void TestFeedAndDbTune<T>(ISyncFeed<T> feed, IDbMeta db, IDbMeta.TuneType? tuneType = null)
    {
        feed.StateChanged += Raise.EventWith(new SyncFeedStateEventArgs(SyncFeedState.Active));

        db.Received().Tune(tuneType ?? _tuneType);

        feed.StateChanged += Raise.EventWith(new SyncFeedStateEventArgs(SyncFeedState.Finished));

        db.Received().Tune(IDb.TuneType.Default);
    }
}
