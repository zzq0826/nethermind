// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core.Collections;

namespace Nethermind.Core;

public class LazyWriteBatchStore: IKeyValueStoreWithBatching
{
    private IKeyValueStoreWithBatching _implementation;
    private ArrayPoolList<(byte[] key, byte[]? value)> _buffer = new(1);
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

    public void Flush()
    {
        if (_buffer.Count == 0) return;

        IBatch baseBatch = _implementation.StartBatch();
        foreach ((byte[] key, byte[]? value) in _buffer)
        {
            baseBatch.Set(key, value, _writeFlags);
        }
        baseBatch.Dispose();

        _buffer.Clear();
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

    private void LazySet(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags)
    {
        _buffer.Add((key.ToArray(), value));
        _writeFlags = flags;
    }
}
