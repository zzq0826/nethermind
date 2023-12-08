// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
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

    public TreePath Append(params byte[]? nibbles)
    {
        if (nibbles == null) return this;

        int length = Length;
        ValueHash256 path = Path;
        Span<byte> span = path.BytesAsSpan;

        // TODO: Optimize this
        foreach (byte nib in nibbles)
        {
            SetNibble(span, nib, length);
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
