// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;

namespace Nethermind.Core.Buffers;

public interface ICappedArrayPool
{
    CappedArray<byte> Rent(int size);

    void Return(CappedArray<byte> buffer);
}

public static class BufferPoolExtensions
{
    public static CappedArray<byte> SafeRentBuffer(this ICappedArrayPool? pool, int size)
    {
        if (pool == null) return new CappedArray<byte>(new byte[size]);
        return pool.Rent(size);
    }

    public static void SafeReturnBuffer(this ICappedArrayPool? pool, CappedArray<byte> buffer)
    {
        pool?.Return(buffer);
    }

    public static CappedArray<byte> RentAndCopy(this ICappedArrayPool? pool, Span<byte> data)
    {
        CappedArray<byte> buffer = pool.SafeRentBuffer(data.Length);
        data.CopyTo(buffer.AsSpan());
        return buffer;
    }
}
