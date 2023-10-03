// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using Nethermind.Core.Collections;

namespace Nethermind.Core;

/// <summary>
/// Implement some optimization for snap sync related to write batch writes.
/// 1. Combine multiple writes batch from contract store request as most contract have very little node to add, so it
///    have fairly small write batches.
/// 2. Split very large batch into smaller batch as large batch will stall rocksdb's concurrent writes.
/// </summary>
public class SnapWriteBatchPacer: IKeyValueStoreWithBatching
{
    /// <summary>
    /// Because of how rocksdb parallelize writes, a large write batch can stall other new concurrent writes, so
    /// we writes the batch in smaller batches. This removes atomicity so its only turned on when NoWAL flag is on.
    /// It does not work as well as just turning on unordered_write, but Snapshot and Iterator can still works.
    /// Also, limit the amount of memory this will use.
    /// </summary>
    private const int MaxKeyBeforeFlush = 128;

    private IKeyValueStoreWithBatching _implementation;
    private IBatch? _currentBatch = null;
    private long _writeCount = 0;
    private WriteFlags _writeFlags = WriteFlags.None;

    public SnapWriteBatchPacer(IKeyValueStoreWithBatching implementation)
    {
        _implementation = implementation;
    }

    public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
    {
        return _implementation.Get(key, flags);
    }

    public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
    {
        _implementation.Set(key, value, flags);
    }

    public IBatch StartBatch()
    {
        return new LazyWriteBatch(this);
    }

    private void LazySet(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags)
    {
        if (_currentBatch == null)
        {
            IBatch newBatch = _implementation.StartBatch();
            if (Interlocked.CompareExchange(ref _currentBatch, newBatch, null) != null)
            {
                newBatch.Dispose();
            }
        }

        _currentBatch!.Set(key, value, flags);
        _writeFlags = flags;

        if (Interlocked.Increment(ref _writeCount) % MaxKeyBeforeFlush == 0)
        {
            Write();
        }
    }

    private void Write()
    {
        IBatch oldBatch = Interlocked.Exchange(ref _currentBatch, _implementation.StartBatch());
        oldBatch?.Dispose();
    }

    public void Flush()
    {
        IBatch oldBatch = Interlocked.Exchange(ref _currentBatch, null);
        oldBatch?.Dispose();
    }

    private class LazyWriteBatch: IBatch
    {
        private readonly SnapWriteBatchPacer _store;

        public LazyWriteBatch(SnapWriteBatchPacer store)
        {
            _store = store;
        }

        public void Dispose()
        {
        }

        public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
        {
            return _store.Get(key, flags);
        }

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
        {
            _store.LazySet(key, value, flags);
        }
    }
}
