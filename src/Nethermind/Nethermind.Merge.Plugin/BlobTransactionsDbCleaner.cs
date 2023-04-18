// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Db;
using Nethermind.Logging;

namespace Nethermind.Merge.Plugin;

public interface IBlobTransactionsDbCleaner : IDisposable
{
    event EventHandler<FinalizeEventArgs> BlocksFinalized;
}

public class BlobTransactionsDbCleaner : IBlobTransactionsDbCleaner
{
    private readonly IManualBlockFinalizationManager _finalizationManager;
    private readonly IDb _blobTransactionsDb;
    private readonly ILogger _logger;
    private long _lastFinalizedBlock = 0;

    public event EventHandler<FinalizeEventArgs>? BlocksFinalized;

    public BlobTransactionsDbCleaner(IManualBlockFinalizationManager finalizationManager, IDb blobTransactionsDb, ILogManager logManager)
    {
        _finalizationManager = finalizationManager ?? throw new ArgumentNullException(nameof(finalizationManager));
        _blobTransactionsDb = blobTransactionsDb ?? throw new ArgumentNullException(nameof(blobTransactionsDb));
        _logger = logManager.GetClassLogger() ?? throw new ArgumentNullException(nameof(logManager));

        _finalizationManager.BlocksFinalized += OnBlocksFinalized;
    }

    private void OnBlocksFinalized(object? sender, FinalizeEventArgs e)
    {

        long newlyFinalizedBlockNumber = e.FinalizingBlock.Number;

        if (newlyFinalizedBlockNumber > _lastFinalizedBlock)
        {
            Task.Run(() => CleanBlobTransactionsDb(newlyFinalizedBlockNumber));
        }
    }

    private void CleanBlobTransactionsDb(long newlyFinalizedBlockNumber)
    {
        try
        {
            for (long i = _lastFinalizedBlock; i < newlyFinalizedBlockNumber; i++)
            {
                if (_blobTransactionsDb.KeyExists(i))
                {
                    _blobTransactionsDb.Delete(i);
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
