// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Runtime.CompilerServices;
using Nethermind.Core.Crypto;

namespace Nethermind.Trie;

public readonly struct TreePath
{
    public readonly ValueHash256 Path;
    public readonly int Length;

    public static TreePath Empty = new TreePath();

    public TreePath(in ValueHash256 path, int length)
    {
        Path = path;
        Length = length;
    }

    public static TreePath FromPath(ReadOnlySpan<byte> pathHash)
    {
        if (pathHash.Length != 32) throw new Exception("Path must be 32 byte");
        return new TreePath(new ValueHash256(pathHash), 64);
    }

    public static TreePath FromNibble(ReadOnlySpan<byte> pathNibbles)
    {
        Span<byte> pathBytes = stackalloc byte[32];
        Nibbles.ToBytesExtra(pathNibbles).CopyTo(pathBytes);

        return new TreePath(new ValueHash256(pathBytes), pathNibbles.Length);
    }

    public TreePath Append(Span<byte> nibbles)
    {
        if (nibbles.Length == 0) return this;
        if (nibbles.Length == 1) return Append(nibbles[0]);

        int length = Length;
        ValueHash256 path = Path;
        Span<byte> pathSpan = path.BytesAsSpan;

        if (length % 2 == 1)
        {
            SetNibble(pathSpan, nibbles[0], length);
            length++;

            nibbles = nibbles[1..];
        }

        int byteLength = nibbles.Length / 2;
        int pathSpanStart = length / 2;
        for (int i = 0; i < byteLength; i++)
        {
            pathSpan[i + pathSpanStart] = Nibbles.ToByte(nibbles[i * 2], nibbles[i * 2 + 1]);
            length+=2;
        }

        if (nibbles.Length % 2 == 1)
        {
            SetNibble(pathSpan, nibbles[^1], length);
            length++;
        }

        return new TreePath(path, length);
    }

    public TreePath Append(byte nib)
    {
        int length = Length;
        ValueHash256 path = Path;
        Span<byte> span = path.BytesAsSpan;

        SetNibble(span, nib, length);
        length++;

        return new TreePath(path, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetNibble(Span<byte> bytes, byte nib, int idx)
    {
        if (idx >= 64) throw new IndexOutOfRangeException();
        if (idx % 2 == 0)
        {
            bytes[idx / 2] =
                (byte)((bytes[idx / 2] & 0x0f) |
                       (nib & 0x0f) << 4);
        }
        else
        {
            bytes[idx / 2] =
                (byte)((bytes[idx / 2] & 0xf0) |
                       ((nib & 0x0f)));
        }
    }

    // TODO: Someone will optimize this
    /*
    public byte this[int childPathLength]
    {
        get
        {
            if (childPathLength >= 64) throw new IndexOutOfRangeException();
            if (childPathLength % 2 == 0)
            {
                return (byte)(Path.Bytes[childPathLength / 2] & 0xf0);
            }
            else
            {
                return (byte)((Path.Bytes[childPathLength / 2] & 0x0f) << 4);
            }
        }
        set
        {
            if (childPathLength >= 64) throw new IndexOutOfRangeException();
            if (childPathLength % 2 == 0)
            {
                Path.BytesAsSpan[childPathLength / 2] =
                    (byte)((Path.Bytes[childPathLength / 2] & 0x0f) |
                           (value & 0x0f) << 4);
            }
            else
            {
                Path.BytesAsSpan[childPathLength / 2] =
                    (byte)((Path.Bytes[childPathLength / 2] & 0xf0) |
                           ((value & 0x0f)));
            }
        }
    }
    */

    public override string ToString()
    {
        return $"{Length} {Path}";
    }
}
