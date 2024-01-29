// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Tracing.GethStyle.JavaScript;
using RocksDbSharp;
using IWriteBatch = Nethermind.Core.IWriteBatch;

namespace Nethermind.Db.Rocks;

public class ColumnDb : IDb
{
    private readonly RocksDb _rocksDb;
    internal readonly DbOnTheRocks _mainDb;
    public readonly ColumnFamilyHandle _columnFamily;

    private readonly DbOnTheRocks.ManagedIterators _readaheadIterators = new();

    public ColumnDb(RocksDb rocksDb, DbOnTheRocks mainDb, string name)
    {
        _rocksDb = rocksDb;
        _mainDb = mainDb;
        if (name == "Default") name = "default";
        _columnFamily = _rocksDb.GetColumnFamily(name);
        Name = name;
    }

    public void Dispose()
    {
        _readaheadIterators.DisposeAll();
    }

    public string Name { get; }

    public byte[]? Get(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
    {
        return _mainDb.GetWithColumnFamily(key, _columnFamily, _readaheadIterators, flags);
    }

    public Span<byte> GetSpan(ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None)
    {
        return _mainDb.GetSpanWithColumnFamily(key, _columnFamily);
    }

    public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
    {
        _mainDb.SetWithColumnFamily(key, _columnFamily, value, flags);
    }

    public void PutSpan(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, WriteFlags writeFlags = WriteFlags.None)
    {
        _mainDb.SetWithColumnFamily(key, _columnFamily, value, writeFlags);
    }

    public KeyValuePair<byte[], byte[]?>[] this[byte[][] keys] =>
        _rocksDb.MultiGet(keys, keys.Select(k => _columnFamily).ToArray());

    public IEnumerable<KeyValuePair<byte[], byte[]?>> GetAll(bool ordered = false)
    {
        Iterator iterator = _mainDb.CreateIterator(ordered, _columnFamily);
        return _mainDb.GetAllCore(iterator).ToArray();
    }

    public IEnumerable<byte[]> GetAllKeys(bool ordered = false)
    {
        Iterator iterator = _mainDb.CreateIterator(ordered, _columnFamily);
        return _mainDb.GetAllKeysCore(iterator).ToArray();
    }

    public IEnumerable<byte[]> GetAllValues(bool ordered = false)
    {
        Iterator iterator = _mainDb.CreateIterator(ordered, _columnFamily);
        return _mainDb.GetAllValuesCore(iterator).ToArray();
    }

    public IWriteBatch StartWriteBatch()
    {
        return new ColumnsDbWriteBatch(this, (DbOnTheRocks.RocksDbWriteBatch)_mainDb.StartWriteBatch());
    }

    private class ColumnsDbWriteBatch : IWriteBatch
    {
        private readonly ColumnDb _columnDb;
        private readonly DbOnTheRocks.RocksDbWriteBatch _underlyingWriteBatch;

        public ColumnsDbWriteBatch(ColumnDb columnDb, DbOnTheRocks.RocksDbWriteBatch underlyingWriteBatch)
        {
            _columnDb = columnDb;
            _underlyingWriteBatch = underlyingWriteBatch;
        }

        public void Dispose()
        {
            _underlyingWriteBatch.Dispose();
        }

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
        {
            if (value is null)
            {
                _underlyingWriteBatch.Delete(key, _columnDb._columnFamily);
            }
            else
            {
                _underlyingWriteBatch.Set(key, value, _columnDb._columnFamily, flags);
            }
        }

        public void PutSpan(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, WriteFlags flags = WriteFlags.None)
        {
            _underlyingWriteBatch.Set(key, value, _columnDb._columnFamily, flags);
        }

        public void Remove(ReadOnlySpan<byte> key)
        {
            // TODO: this does not participate in batching?
            _underlyingWriteBatch.Delete(key, _columnDb._columnFamily);
        }
    }

    public unsafe void Remove(ReadOnlySpan<byte> key)
    {
        fixed (byte* bytes = &MemoryMarshal.GetReference(key))
        {
            var opts = new WriteOptions();
            Native.Instance.rocksdb_delete_cf(_rocksDb.Handle, opts.Handle, _columnFamily.Handle, bytes, (nuint)key.Length);
        }
    }

    //public unsafe void RemoveRange(ReadOnlySpan<byte> key, ReadOnlySpan<byte> keyEnd)
    //{
    //    // TODO: this does not participate in batching?
    //    fixed (byte* key2 = &MemoryMarshal.GetReference(key))
    //    {
    //        fixed (byte* key2End = &MemoryMarshal.GetReference(keyEnd))
    //        {
    //            Native.Instance.rocksdb_delete_range_cf(_rocksDb.Handle, opts.Handle, _columnFamily.Handle, key2, (nuint)key.Length, key2End, (nuint)keyEnd.Length);
    //        }
    //    }
    //}
    //public unsafe void Remove(ReadOnlySpan<byte> key)
    //{
    //    // TODO: this does not participate in batching?

    //    fixed (byte* key2 = &MemoryMarshal.GetReference(key))
    //    {
    //        if (_columnFamily == null)
    //        {
    //            Native.Instance.rocksdb_singledelete(_rocksDb.Handle, (_mainDb.WriteOptions ?? new RocksDbSharp.WriteOptions()).Handle, key2, (nuint)key.Length);
    //        }
    //        else
    //        {
    //            Native.Instance.rocksdb_singledelete(_rocksDb.Handle, (_mainDb.WriteOptions ?? new RocksDbSharp.WriteOptions()).Handle, _columnFamily.Handle, key2, (nuint)key.Length);
    //        }
    //    }
    //}

    public bool KeyExists(ReadOnlySpan<byte> key) => _rocksDb.Get(key, _columnFamily) is not null;

    public void Flush()
    {
        var opts = Native.Instance.rocksdb_flushoptions_create();
        Native.Instance.rocksdb_flush_cf(_rocksDb.Handle, opts, _columnFamily.Handle);
        Native.Instance.rocksdb_flushoptions_destroy(opts);
    }

    public void Compact()
    {
        _rocksDb.CompactRange(Keccak.Zero.BytesToArray(), Keccak.MaxValue.BytesToArray(), _columnFamily);
    }

    public unsafe void DeleteRange(byte[] start, byte[] end)
    {
        fixed (byte* converted = start)
        {
            fixed (byte* converted2 = end)
            {
                var w = new WriteOptions();
                this._mainDb._rocksDbNative.rocksdb_delete_range_cf(_rocksDb.Handle, w.Handle, this._columnFamily.Handle, converted, (nuint)start.Length, converted2, (nuint)end.Length);
                this._mainDb._rocksDbNative.rocksdb_suggest_compact_range_cf(_rocksDb.Handle, this._columnFamily.Handle, converted, (nuint)start.Length, converted2, (nuint)end.Length);
            }
        }
    }
    /// <summary>
    /// Not sure how to handle delete of the columns DB
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    public void Clear() { throw new NotSupportedException(); }
    public long GetSize() => _mainDb.GetSize();
    public long GetCacheSize() => _mainDb.GetCacheSize();
    public long GetIndexSize() => _mainDb.GetIndexSize();
    public long GetMemtableSize() => _mainDb.GetMemtableSize();

    public void DangerousReleaseMemory(in ReadOnlySpan<byte> span)
    {
        _mainDb.DangerousReleaseMemory(span);
    }

    public void CompactRange(long keyFrom, long keyTo)
    {
        _rocksDb.CompactRange(BitConverter.GetBytes(keyFrom), BitConverter.GetBytes(keyTo));
    }
}
