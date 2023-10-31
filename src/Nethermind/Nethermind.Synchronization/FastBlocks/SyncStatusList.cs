// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Caching;

namespace Nethermind.Synchronization.FastBlocks
{
    abstract class SyncStatusList: ISyncStatusList
    {
        private long _queueSize;
        protected readonly IBlockTree _blockTree;
        private FastBlockStatusList _statuses;
        private readonly LruCache<long, BlockInfo> _cache = new(maxCapacity: 64, startCapacity: 64, "blockInfo Cache");
        private long _lowestInsertWithoutGaps;
        private long _lowerBound;

        public long LowestInsertWithoutGaps
        {
            get => _lowestInsertWithoutGaps;
            private set => _lowestInsertWithoutGaps = value;
        }

        public long QueueSize => _queueSize;

        protected SyncStatusList(IBlockTree blockTree)
        {
            _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
        }

        protected void Reset(
            long pivotNumber,
            long? lowestInserted,
            long lowerBound)
        {
            _statuses = new FastBlockStatusList(pivotNumber + 1);
            LowestInsertWithoutGaps = lowestInserted ?? pivotNumber;
            _lowerBound = lowerBound;
        }

        public void GetInfosForBatch(BlockInfo?[] blockInfos)
        {
            GetInfosForBatch(
                blockInfos,
                FastBlockStatus.BodiesRequestSent,
                FastBlockStatus.BodiesInserted,
                _lowerBound,
                ref _lowestInsertWithoutGaps,
                ref _queueSize
            );
        }

        protected void GetInfosForBatch(
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

        public void MarkInserted(long blockNumber)
        {
            if (_statuses.TrySet(blockNumber, FastBlockStatus.BodiesInserted))
            {
                Interlocked.Increment(ref _queueSize);
            }
        }

        public abstract void Reset();

        public void MarkPending(BlockInfo blockInfo)
        {
            if (_statuses.TrySet(blockInfo.BlockNumber, FastBlockStatus.BodiesPending))
            {
                _cache.Set(blockInfo.BlockNumber, blockInfo);
            }
        }
    }
}
