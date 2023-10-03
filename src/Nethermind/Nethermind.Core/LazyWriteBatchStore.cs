// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using Nethermind.Core.Collections;

namespace Nethermind.Core;

public class LazyWriteBatchStore: IKeyValueStoreWithBatching
{
    /// <summary>
    /// Because of how rocksdb parallelize writes, a large write batch can stall other new concurrent writes, so
    /// we writes the batch in smaller batches. This removes atomicity so its only turned on when NoWAL flag is on.
    /// It does not work as well as just turning on unordered_write, but Snapshot and Iterator can still works.
    /// </summary>
    private const int MaxKeyBeforeFlush = 128;

    private IKeyValueStoreWithBatching _implementation;
    private ArrayPoolList<(byte[] key, byte[]? value)> _buffer = new(MaxKeyBeforeFlush);
    private WriteFlags _writeFlags = WriteFlags.None;

    public LazyWriteBatchStore(IKeyValueStoreWithBatching implementation)
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
        _buffer.Add((key.ToArray(), value));
        _writeFlags = flags;

        if (_buffer.Count >= MaxKeyBeforeFlush)
        {
            Flush();
        }
    }

    private void Flush()
    {
        if (_buffer.Count == 0) return;
        using ArrayPoolList<(byte[] key, byte[]? value)> prevBuffer = Interlocked.Exchange(ref _buffer,
            new ArrayPoolList<(byte[] key, byte[]? value)>(MaxKeyBeforeFlush));
        if (prevBuffer.Count == 0) return;

        IBatch baseBatch = _implementation.StartBatch();
        foreach ((byte[] key, byte[]? value) in prevBuffer)
        {
            baseBatch.Set(key, value, _writeFlags);
        }

        baseBatch.Dispose();
    }

    private class LazyWriteBatch: IBatch
    {
        private readonly LazyWriteBatchStore _store;

        public LazyWriteBatch(LazyWriteBatchStore store)
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
