// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Db;
using Nethermind.Logging;

namespace Nethermind.Merge.Plugin;

public class BlobTransactionsDbCleaner : IDisposable
{
    private readonly IManualBlockFinalizationManager _finalizationManager;
    private readonly IDb _blobTransactionsDb;
    private readonly ILogger _logger;
    private long _lastFinalizedBlock = 0;

    public BlobTransactionsDbCleaner(IManualBlockFinalizationManager finalizationManager, IDb blobTransactionsDb, ILogManager logManager)
    {
        _finalizationManager = finalizationManager ?? throw new ArgumentNullException(nameof(finalizationManager));
        _blobTransactionsDb = blobTransactionsDb ?? throw new ArgumentNullException(nameof(blobTransactionsDb));
        _logger = logManager.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));

        _finalizationManager.BlocksFinalized += OnBlocksFinalized;
    }

    private void OnBlocksFinalized(object? sender, FinalizeEventArgs e)
    {
        if (e.FinalizingBlock.Number > _lastFinalizedBlock)
        {
            Task.Run(() => CleanBlobTransactionsDb(e.FinalizingBlock.Number));
        }
    }

    private void CleanBlobTransactionsDb(long newlyFinalizedBlockNumber)
    {
        try
        {
            if (_lastFinalizedBlock != 0)
            {
                for (long i = _lastFinalizedBlock + 1; i <= newlyFinalizedBlockNumber; i++)
                {
                    if (_blobTransactionsDb.KeyExists(i))
                    {
                        _blobTransactionsDb.Delete(i);
                    }
                }
            }

            _lastFinalizedBlock = newlyFinalizedBlockNumber;
        }
        catch (Exception exception)
        {
            if (_logger.IsError) _logger.Error($"Couldn't correctly clean blob transactions db. Newly finalized block {newlyFinalizedBlockNumber}, last finalized block: {_lastFinalizedBlock}", exception);
        }
    }

    public void Dispose()
    {
        _finalizationManager.BlocksFinalized -= OnBlocksFinalized;
    }
}
