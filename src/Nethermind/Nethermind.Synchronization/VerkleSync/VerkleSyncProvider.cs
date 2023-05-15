// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Microsoft.Extensions.ObjectPool;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Synchronization.SnapSync;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.Sync;
using ILogger = Nethermind.Logging.ILogger;

namespace Nethermind.Synchronization.VerkleSync;

public class VerkleSyncProvider: IVerkleSyncProvider
{
    private readonly ObjectPool<IVerkleStore> _trieStorePool;
    private readonly IDbProvider _dbProvider;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;

    private readonly VerkleProgressTracker _progressTracker;

    public VerkleSyncProvider(VerkleProgressTracker progressTracker, IDbProvider dbProvider, ILogManager logManager)
    {
        _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        _progressTracker = progressTracker ?? throw new ArgumentNullException(nameof(progressTracker));
        _trieStorePool = new DefaultObjectPool<IVerkleStore>(new TrieStorePoolPolicy(_dbProvider, logManager));

        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        _logger = logManager.GetClassLogger();
    }

    public bool CanSync() => _progressTracker.CanSync();

    public AddRangeResult AddSubTreeRange(SubTreeRange request, SubTreesAndProofs response)
    {
        throw new NotImplementedException();
    }

    public AddRangeResult AddSubTreeRange(long blockNumber, byte[] expectedRootHash, byte[] startingStem,
        PathWithSubTree[] subTrees, byte[][] proofs = null, byte[] limitStem = null)
    {
        throw new NotImplementedException();
    }

    public void RefreshLeafs(LeafToRefreshRequest request, byte[][] response)
    {
        throw new NotImplementedException();
    }

    private void RetryLeafRefresh(byte[] leaf)
    {
        _progressTracker.EnqueueLeafRefresh(leaf);
    }

    public void RetryRequest(VerkleSyncBatch batch)
    {
        if (batch.SubTreeRangeRequest is not null)
        {
            _progressTracker.ReportSubTreeRangePartitionFinished(batch.SubTreeRangeRequest.LimitStem);
        }
        else if (batch.LeafToRefreshRequest is not null)
        {
            _progressTracker.ReportLeafRefreshFinished(batch.LeafToRefreshRequest);
        }
    }

    public bool IsVerkleGetRangesFinished() => _progressTracker.IsVerkleGetRangesFinished();

    public void UpdatePivot()
    {
        _progressTracker.UpdatePivot();
    }

    public (VerkleSyncBatch request, bool finished) GetNextRequest() => _progressTracker.GetNextRequest();

    private class TrieStorePoolPolicy : IPooledObjectPolicy<IVerkleStore>
    {
        private readonly IDbProvider _dbProvider;
        private readonly ILogManager _logManager;

        public TrieStorePoolPolicy(IDbProvider provider, ILogManager logManager)
        {
            _dbProvider = provider;
            _logManager = logManager;
        }

        public IVerkleStore Create()
        {
            return new VerkleStateStore(_dbProvider, 0);
        }

        public bool Return(IVerkleStore obj)
        {
            return true;
        }
    }
}
