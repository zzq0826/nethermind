// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Pruning;

public class NodeStorage : INodeStorage
{
    protected readonly IKeyValueStore _keyValueStore;
    private static byte[] EmptyTreeHashBytes = new byte[] { 128 };
    public const int StoragePathLength = 48;

    public NodeStorage(IKeyValueStore keyValueStore)
    {
        _keyValueStore = keyValueStore ?? throw new ArgumentNullException(nameof(keyValueStore));
    }

    public static byte[] GetNodeStoragePath(Hash256? address, in TreePath path, in ValueHash256 keccak)
    {
        byte[] bytes = new byte[StoragePathLength];
        GetNodeStoragePathSpan(bytes, address, path, keccak);
        return bytes;
    }

    public Span<byte> GetPath(Span<byte> pathSpan, Hash256? address, in TreePath path, in ValueHash256 keccak)
    {
        return GetNodeStoragePathSpan(pathSpan, address, path, keccak);
    }

    private static Span<byte> GetNodeStoragePathSpan(Span<byte> pathSpan, Hash256? address, in TreePath path, in ValueHash256 keccak)
    {
        Debug.Assert(pathSpan.Length == StoragePathLength);

        if (address == null) {
            path.Path.BytesAsSpan[..15].CopyTo(pathSpan[1..]);

            // Separate the top level tree into its own section. This improve cache hit rate by about a few %. The idea
            // being that the top level trie is spread out across the database, leading for a poor cache hit. In practice
            // its not that much, likely because they are also likely to be at the top of LSM tree.
            // The total size of node for path length<=4 should be around 40MB making them very cacheable. In practice
            // whether they get cached or not is seemingly random. This leaves the remaining subtree size of higher depth
            // to be about 44kB meaning they should take at most 2 iops on 16kB block size. Trying to make this tree
            // split at depth 5 does not seems to improve things even with more cache.
            if (path.Length <= 4)
            {
                pathSpan[0] = 0;
            }
            else
            {
                pathSpan[0] = 1;
            }
        }
        else
        {
            // Technically, you'll need 9 byte for address and 8 byte for storage on mainnet. But we want to keep
            // key small at the same time too. If the key are too small, multiple node will be out of order, which
            // can be slower but as long as they are in the same data block, it should not make a difference.
            pathSpan[0] = 2;
            address.Bytes[..8].CopyTo(pathSpan[1..]);
            path.Path.BytesAsSpan[..7].CopyTo(pathSpan[9..]);
        }

        keccak.Bytes.CopyTo(pathSpan[16..]);

        return pathSpan;
    }

    public byte[]? Get(Hash256? address, in TreePath path, in ValueHash256 keccak, ReadFlags readFlags = ReadFlags.None)
    {
        if (keccak == Keccak.EmptyTreeHash)
        {
            return EmptyTreeHashBytes;
        }

        Span<byte> storagePathSpan = stackalloc byte[StoragePathLength];
        return _keyValueStore.Get(GetPath(storagePathSpan, address, path, keccak), readFlags);
    }

    public bool KeyExists(Hash256? address, in TreePath path, in ValueHash256 keccak)
    {
        if (keccak == Keccak.EmptyTreeHash)
        {
            return true;
        }

        Span<byte> storagePathSpan = stackalloc byte[StoragePathLength];
        return _keyValueStore.KeyExists(GetPath(storagePathSpan, address, path, keccak));
    }

    public INodeWriteBatch StartWriteBatch()
    {
        return new NodeWriteBatch(((IKeyValueStoreWithBatching)_keyValueStore).StartWriteBatch(), this);
    }

    public void Set(Hash256? address, in TreePath path, in ValueHash256 keccak, byte[] toArray, WriteFlags writeFlags = WriteFlags.None)
    {
        if (keccak == Keccak.EmptyTreeHash)
        {
            return;
        }

        Span<byte> storagePathSpan = stackalloc byte[StoragePathLength];
        _keyValueStore.Set(GetPath(storagePathSpan, address, path, keccak), toArray, writeFlags);
    }

    public byte[]? GetByHash(ReadOnlySpan<byte> key, ReadFlags flags)
    {
        return _keyValueStore.Get(key, flags);
    }

    public void Flush()
    {
        if (_keyValueStore is IDb db)
        {
            db.Flush();
        }
    }
}

public interface INodeStorage
{
    byte[]? Get(Hash256? address, in TreePath path, in ValueHash256 keccak, ReadFlags readFlags = ReadFlags.None);
    void Set(Hash256? address, in TreePath path, in ValueHash256 hash, byte[] toArray, WriteFlags writeFlags = WriteFlags.None);
    bool KeyExists(Hash256? address, in TreePath path, in ValueHash256 hash);
    INodeWriteBatch StartWriteBatch();

    byte[]? GetByHash(ReadOnlySpan<byte> key, ReadFlags flags);
    void Flush();
}

public interface INodeWriteBatch : IDisposable
{
    void Remove(Hash256? address, in TreePath path, in ValueHash256 persistedHash);
    void Set(Hash256? address, in TreePath path, in ValueHash256 currentNodeKeccak, byte[] toArray, WriteFlags writeFlags);
}

public class NodeWriteBatch : INodeWriteBatch
{
    private readonly IWriteBatch _writeBatch;
    private readonly NodeStorage _nodeStorage;

    public NodeWriteBatch(IWriteBatch writeBatch, NodeStorage nodeStorage)
    {
        _writeBatch = writeBatch;
        _nodeStorage = nodeStorage;
    }

    public void Dispose()
    {
        _writeBatch.Dispose();
    }

    public void Remove(Hash256? address, in TreePath path, in ValueHash256 keccak)
    {
        if (keccak == Keccak.EmptyTreeHash)
        {
            return;
        }

        Span<byte> storagePathSpan = stackalloc byte[NodeStorage.StoragePathLength];
        _writeBatch.Remove(_nodeStorage.GetPath(storagePathSpan, address, path, keccak));
    }

    public void Set(Hash256? address, in TreePath path, in ValueHash256 keccak, byte[] toArray, WriteFlags writeFlags)
    {
        if (keccak == Keccak.EmptyTreeHash)
        {
            return;
        }

        Span<byte> storagePathSpan = stackalloc byte[NodeStorage.StoragePathLength];
        _writeBatch.Set(_nodeStorage.GetPath(storagePathSpan, address, path, keccak), toArray, writeFlags);
    }
}
