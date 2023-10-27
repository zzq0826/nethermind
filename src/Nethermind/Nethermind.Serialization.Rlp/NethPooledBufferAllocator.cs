// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using DotNetty.Buffers;
using Nethermind.Core.Extensions;

namespace Nethermind.Serialization.Rlp;

/// <summary>
/// Split allocation to two underlying allocator, one for small and one for large. The idea is that, if mixed, the
/// chunk created by large allocation even if largely free would have a hard time being released back due to the use of
/// thread cache which holds some of the small allocation in threadlocal. Additionally, you'd want to have a lot of
/// small arena to reduce lock contention as they are used a lot, but you don't want a lot of large arena as they will
/// spread the memory too much and unable to pool large allocation at all.
/// </summary>
public class NethPooledBufferAllocator : AbstractByteBufferAllocator
{
    private static NethPooledBufferAllocator? _instance = null;
    public static NethPooledBufferAllocator Instance => _instance ??= new NethPooledBufferAllocator(1, 11); // The default is in unit tests only

    // 32KB is the maximum allocation size that is cached in threadcache
    private readonly int SizeThreshold = (int)32.KiB();

    private IByteBufferAllocator _notSmallAllocator;
    private IByteBufferAllocator _smallAllocator;

    private NethPooledBufferAllocator(uint arenaCount, int arenaOrder)
    {
        // Small allocator. Does not allocate much in term of space, but majority of allocation is here. Probably need
        // to be fast. Here, take 1MB each core.
        _smallAllocator = new PooledByteBufferAllocator(
            Environment.ProcessorCount, Environment.ProcessorCount, 8192, 7
        );

        // Large allocator. Minimum allocation is 32.KiB. Use a lot of space. No threadcache, so if buffer is released,
        // chunk should be released easier.
        _notSmallAllocator = new PooledByteBufferAllocator(
            (int)arenaCount, (int)arenaCount, 8192, arenaOrder,
            // No thread cache! Well.. allocation here would be more than 32KB so wont get cached anyway...
            tinyCacheSize: 0, smallCacheSize: 0, normalCacheSize: 0);
    }

    public static void Configure(uint arenaCount, int arenaOrder)
    {
        _instance = new NethPooledBufferAllocator(arenaCount, arenaOrder);
    }

    protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity)
    {
        return initialCapacity > SizeThreshold ? _notSmallAllocator.HeapBuffer(initialCapacity, maxCapacity) : _smallAllocator.HeapBuffer(initialCapacity, maxCapacity);
    }

    protected override IByteBuffer NewDirectBuffer(int initialCapacity, int maxCapacity)
    {
        return initialCapacity > SizeThreshold ? _notSmallAllocator.DirectBuffer(initialCapacity, maxCapacity) : _smallAllocator.DirectBuffer(initialCapacity, maxCapacity);
    }

    public override bool IsDirectBufferPooled => _notSmallAllocator.IsDirectBufferPooled;
}
