// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Core.Verkle;

[DebuggerStepThrough]
public static class Pedersen
{
    public const int Size = 32;

    public const int MemorySize =
        MemorySizes.SmallObjectOverhead -
        MemorySizes.RefSize +
        Size;

    /// <returns>
    ///     <string>0x0000000000000000000000000000000000000000000000000000000000000000</string>
    /// </returns>
    public static readonly Hash256 Zero = new(new byte[Size]);

    /// <summary>
    ///     <string>0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff</string>
    /// </summary>
    public static Hash256 MaxValue { get; } = new("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");

    [DebuggerStepThrough]
    public static Hash256 Compute(ReadOnlySpan<byte> address20, UInt256 treeIndex)
    {
        return new Hash256(PedersenHash.ComputeHashBytes(address20, treeIndex));
    }

    [DebuggerStepThrough]
    public static Hash256 Compute(byte[] address20, UInt256 treeIndex)
    {
        return new Hash256(PedersenHash.ComputeHashBytes(address20, treeIndex));
    }
}
