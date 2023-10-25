// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Buffers;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Blockchain.Blocks;

public class BlockStore : IBlockStore
{
    private readonly IKeyValueStore _blockDb;
    private readonly IKeyValueStore _metadataDb;
    private readonly BlockDecoder _blockDecoder = new();
    private const int CacheSize = 128 + 32;

    private static readonly byte[] MetadataKeyPrefix = Keccak.Compute("blockmetadata").BytesToArray();

    private readonly LruCache<ValueKeccak, Block>
        _blockCache = new(CacheSize, CacheSize, "blocks");

    public BlockStore(IKeyValueStore blockDb, IKeyValueStore metadataDb)
    {
        _blockDb = blockDb;
        _metadataDb = metadataDb;
    }

    public void SetMetadata(byte[] key, byte[] value)
    {
        _metadataDb.Set(Bytes.Concat(MetadataKeyPrefix, key), value);
    }

    public byte[]? GetMetadata(byte[] key)
    {
        byte[]? result = _metadataDb.Get(Bytes.Concat(MetadataKeyPrefix, key));
        return result ?? _blockDb.Get(key);
    }

    public void Insert(Block block)
    {
        if (block.Hash is null)
        {
            throw new InvalidOperationException("An attempt to store a block with a null hash.");
        }

        // if we carry Rlp from the network message all the way here then we could solve 4GB of allocations and some processing
        // by avoiding encoding back to RLP here (allocations measured on a sample 3M blocks Goerli fast sync
        using NettyRlpStream newRlp = _blockDecoder.EncodeToNewNettyStream(block);

        _blockDb.Set(block.Number, block.Hash, newRlp.AsSpan());
    }

    private static void GetBlockNumPrefixedKey(long blockNumber, Keccak blockHash, Span<byte> output)
    {
        blockNumber.WriteBigEndian(output);
        blockHash!.Bytes.CopyTo(output[8..]);
    }

    public void Delete(long blockNumber, Keccak blockHash)
    {
        _blockCache.Delete(blockHash);
        _blockDb.Delete(blockNumber, blockHash);
        _blockDb.Remove(blockHash.Bytes);
    }

    public Block? Get(long blockNumber, Keccak blockHash, bool shouldCache = false)
    {
        Block? b = _blockDb.Get(blockNumber, blockHash, _blockDecoder, _blockCache, shouldCache);
        if (b != null) return b;
        return _blockDb.Get(blockHash, _blockDecoder, _blockCache, shouldCache);
    }

    public ReceiptRecoveryBlock? GetReceiptRecoveryBlock(long blockNumber, Keccak blockHash)
    {
        Span<byte> keyWithBlockNumber = stackalloc byte[40];
        GetBlockNumPrefixedKey(blockNumber, blockHash, keyWithBlockNumber);

        MemoryManager<byte>? memoryOwner = _blockDb.GetOwnedMemory(keyWithBlockNumber);
        if (memoryOwner == null)
        {
            memoryOwner = _blockDb.GetOwnedMemory(blockHash.Bytes);
        }

        return _blockDecoder.DecodeToReceiptRecoveryBlock(memoryOwner, memoryOwner?.Memory ?? Memory<byte>.Empty, RlpBehaviors.None);
    }

    public void Cache(Block block)
    {
        _blockCache.Set(block.Hash, block);
    }
}
