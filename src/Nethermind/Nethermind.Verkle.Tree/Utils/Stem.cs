// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;
using Nethermind.Core.Crypto;

namespace Nethermind.Verkle.Tree.Utils;

public unsafe struct Stem
{
    internal const int Size = 31;
    public fixed byte Bytes[Size];

    public Span<byte> BytesAsSpan => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref this, 1));
}


//
// ValueKeccak result = new();
// byte* ptr = result.Bytes;
// Span<byte> output = new(ptr, KeccakHash.HASH_SIZE);
// KeccakHash.ComputeHashBytesToSpan(input, output);
