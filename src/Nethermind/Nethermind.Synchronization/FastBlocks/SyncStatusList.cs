// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Core.Caching;

namespace Nethermind.Synchronization.FastBlocks
{
    public class SyncStatusList
    {
        private readonly ISyncConfig _syncConfig;
        private readonly IBlockTree _blockTree;
        private readonly IReceiptStorage _receiptStorage;
        private readonly LruCache<long, BlockInfo> _cache = new(maxCapacity: 64, startCapacity: 64, "blockInfo Cache");

        private FastBlockStatusList _statuses;

        private long _bodiesQueueSize;
        private long _bodiesLowestInsertWithoutGaps;
        private long _bodiesLowerBound;

        private long _receiptsQueueSize;
        private long _receiptsLowestInsertWithoutGaps;
        private long _receiptsLowerBound;

        public SyncStatusList(IBlockTree blockTree, IReceiptStorage receiptStorage, ISyncConfig syncConfig)
        {
            _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
            _syncConfig = syncConfig ?? throw new ArgumentNullException(nameof(syncConfig));
            _receiptStorage = receiptStorage ?? throw new ArgumentNullException(nameof(receiptStorage));
        }

        public void Reset()
        {
            long pivotNumber = _syncConfig.PivotNumberParsed;
            _statuses = new FastBlockStatusList(pivotNumber + 1);

            BodiesLowestInsertWithoutGaps = _blockTree.LowestInsertedBodyNumber ?? pivotNumber;
            _bodiesLowerBound = _syncConfig.AncientBodiesBarrier;

            ReceiptsLowestInsertWithoutGaps = _receiptStorage.LowestInsertedReceiptBlockNumber ?? pivotNumber;
            _receiptsLowerBound = _syncConfig.AncientReceiptsBarrier;

            if (ReceiptsLowestInsertWithoutGaps != 0 && ReceiptsLowestInsertWithoutGaps >= _receiptsLowerBound)
            {
                // Need to fill it with bodies inserted or receipts won't start.
                for (long i = pivotNumber; i > BodiesLowestInsertWithoutGaps; i--)
                {
                    _statuses[i] = FastBlockStatus.BodiesInserted;
                }
            }
        }

        private void GetInfosForBatch(
            BlockInfo?[] blockInfos,
            FastBlockStatus sentStatus,
            FastBlockStatus insertedStatus,
            long lowerBound,
            ref long lowestWithoutGapVar,
            ref long queueSizeVar
        )
        {
            int collected = 0;
            long currentNumber = Volatile.Read(ref lowestWithoutGapVar);
            while (collected < blockInfos.Length && currentNumber != 0 && currentNumber >= lowerBound)
            {
                if (blockInfos[collected] is not null)
                {
                    collected++;
                    continue;
                }

                if (_statuses.TrySet(currentNumber, sentStatus, out FastBlockStatus status))
                {
                    if (_cache.TryGet(currentNumber, out BlockInfo blockInfo))
                    {
                        _cache.Delete(currentNumber);
                    }
                    else
                    {
                        blockInfo = _blockTree.FindCanonicalBlockInfo(currentNumber);
                    }

                    blockInfos[collected] = blockInfo;
                    collected++;
                }
                else if (status == insertedStatus)
                {
                    long currentLowest = Volatile.Read(ref lowestWithoutGapVar);
                    if (currentNumber == currentLowest)
                    {
                        if (Interlocked.CompareExchange(ref lowestWithoutGapVar, currentLowest - 1, currentLowest) == currentLowest)
                        {
                            Interlocked.Decrement(ref queueSizeVar);
                        }
                    }
                }

                currentNumber--;
            }
        }

        #region Bodies

        public long BodiesLowestInsertWithoutGaps
        {
            get => _bodiesLowestInsertWithoutGaps;
            private set => _bodiesLowestInsertWithoutGaps = value;
        }

        public long BodiesQueueSize => _bodiesQueueSize;

        public void GetInfosForBodiesBatch(BlockInfo?[] blockInfos)
        {
            GetInfosForBatch(
                blockInfos,
                FastBlockStatus.BodiesRequestSent,
                FastBlockStatus.BodiesInserted,
                _bodiesLowerBound,
                ref _bodiesLowestInsertWithoutGaps,
                ref _bodiesQueueSize
            );
        }

        public void MarkInsertedBody(long blockNumber)
        {
            if (_statuses.TrySet(blockNumber, FastBlockStatus.BodiesInserted))
            {
                Interlocked.Increment(ref _bodiesQueueSize);
            }
        }

        public void MarkPendingBody(BlockInfo blockInfo)
        {
            if (_statuses.TrySet(blockInfo.BlockNumber, FastBlockStatus.BodiesPending))
            {
                _cache.Set(blockInfo.BlockNumber, blockInfo);
            }
        }

        #endregion


        #region Receipts

        public long ReceiptsLowestInsertWithoutGaps
        {
            get => _receiptsLowestInsertWithoutGaps;
            private set => _receiptsLowestInsertWithoutGaps = value;
        }

        public long ReceiptsQueueSize => _receiptsQueueSize;

        public void GetInfosForReceiptsBatch(BlockInfo?[] blockInfos)
        {
            GetInfosForBatch(
                blockInfos,
                FastBlockStatus.ReceiptRequestSent,
                FastBlockStatus.ReceiptInserted,
                _receiptsLowerBound,
                ref _receiptsLowestInsertWithoutGaps,
                ref _receiptsQueueSize
            );
        }

        public void MarkInsertedReceipt(long blockNumber)
        {
            if (_statuses.TrySet(blockNumber, FastBlockStatus.ReceiptInserted))
            {
                Interlocked.Increment(ref _receiptsQueueSize);
            }
        }

        public void MarkPendingReceipt(BlockInfo blockInfo)
        {
            if (_statuses.TrySet(blockInfo.BlockNumber, FastBlockStatus.BodiesInserted))
            {
                _cache.Set(blockInfo.BlockNumber, blockInfo);
            }
        }

        #endregion
    }
}
