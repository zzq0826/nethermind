// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using DotNetty.Buffers;

namespace Nethermind.Core.Buffers;

public class NethPooledBuffer : AbstractByteBufferAllocator
{
    private static NethPooledBuffer? _instance = null;
    public static NethPooledBuffer Instance = _instance ?? new NethPooledBuffer(1, 11); // The default is in unit tests only

    // I guess strictly speaking, we can set this higher. This size is picked because this is the threshold for
    // when an allocation is considered "normal" in netty buffer allocation, which is also the page size. Any smaller
    // than this, a special allocation type goes into effect that further split the page.
    private const int SizeThreshold = 8192;

    private IByteBufferAllocator _notSmallAllocator;
    private IByteBufferAllocator _smallAllocator;

    private NethPooledBuffer(uint arenaCount, int arenaOrder)
    {
        // Small allocator. Does not allocate much. Probably need to be fast. Here, take 1MB each thread.
        _smallAllocator = new PooledByteBufferAllocator(
            Environment.ProcessorCount, Environment.ProcessorCount, 8192, 7
        );

        // Default value is 64. Only effect allocation <= 32KB. We don't want thread cache here.
        int normalCacheSize = 2;
        _notSmallAllocator = new PooledByteBufferAllocator(
            (int)arenaCount, (int)arenaCount, 8192, arenaOrder,
            tinyCacheSize: 0, smallCacheSize: 0, normalCacheSize: normalCacheSize);
    }

    public static void Configure(uint arenaCount, int arenaOrder)
    {
        _instance = new NethPooledBuffer(arenaCount, arenaOrder);
    }

    protected override IByteBuffer NewHeapBuffer(int initialCapacity, int maxCapacity)
    {
        return initialCapacity >= SizeThreshold ? _notSmallAllocator.HeapBuffer(initialCapacity, maxCapacity) : _smallAllocator.HeapBuffer(initialCapacity, maxCapacity);
    }

    protected override IByteBuffer NewDirectBuffer(int initialCapacity, int maxCapacity)
    {
        return initialCapacity >= SizeThreshold ? _notSmallAllocator.DirectBuffer(initialCapacity, maxCapacity) : _smallAllocator.DirectBuffer(initialCapacity, maxCapacity);
    }

    public override bool IsDirectBufferPooled => _notSmallAllocator.IsDirectBufferPooled;
}
