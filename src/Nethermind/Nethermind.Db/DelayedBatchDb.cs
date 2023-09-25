// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;

namespace Nethermind.Db;

public class DelayedBatchDb: IKeyValueStoreWithBatching
{
    private IKeyValueStoreWithBatching _implementation;
    private DelayedBatch? _currentBatch = null;

    public DelayedBatchDb(IKeyValueStoreWithBatching implementation)
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
        if (_currentBatch == null)
        {
            _currentBatch = new DelayedBatch(_implementation.StartBatch());
        }
        return  _currentBatch;
    }

    public void FlushBatch()
    {
        _currentBatch?.ActuallyDispose();
        _currentBatch = null;
    }

    private class DelayedBatch : IBatch
    {
        private IBatch _implementation;

        public DelayedBatch(IBatch implementation)
        {
            _implementation = implementation;
        }

        public void Dispose()
        {
        }

        public void ActuallyDispose()
        {
            _implementation.Dispose();
        }

        public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
        {
            return _implementation.Get(key, flags);
        }

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
        {
            _implementation.Set(key, value, flags);
        }
    }
}
