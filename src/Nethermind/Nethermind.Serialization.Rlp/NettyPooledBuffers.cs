// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using DotNetty.Buffers;

namespace Nethermind.Core.Buffers;

public class NethPooledBuffer : AbstractByteBufferAllocator
{
    public static NethPooledBuffer Instance = new NethPooledBuffer();

    private const int SizeThreshold = 8096;

    private IByteBufferAllocator _notSmallAllocator = new PooledByteBufferAllocator();
    private IByteBufferAllocator _smallAllocator = PooledByteBufferAllocator.Default;

    private NethPooledBuffer()
    {
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
