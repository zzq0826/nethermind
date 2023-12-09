// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Nethermind.Core.Extensions;
using NUnit.Framework;

namespace Nethermind.Trie.Test;

public class TreePathTests
{
    [Test]
    public void TestAppend()
    {
        TreePath path = new TreePath();
        for (int i = 0; i < 64; i++)
        {
            path = path.Append((byte)(i % 16));
        }

        string asHex = path.Path.Bytes.ToHexString();
        asHex.Should().Be("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    [Test]
    public void TestAppendArray()
    {
        byte[] nibbles = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            nibbles[i] = (byte)(i % 16);
        }
        TreePath path = new TreePath();
        TreePath newPath = path.Append(nibbles);

        path.Length.Should().Be(0);
        newPath.Length.Should().Be(64);
        string asHex = newPath.Path.Bytes.ToHexString();
        asHex.Should().Be("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    [TestCase(1)]
    [TestCase(11)]
    [TestCase(20)]
    [TestCase(40)]
    [TestCase(41)]
    public void TestAppendArrayDivided(int divisor)
    {
        byte[] nibbles = new byte[64];
        for (int i = 0; i < 64; i++)
        {
            nibbles[i] = (byte)(i % 16);
        }
        TreePath path = new TreePath();
        path = path.Append(nibbles[..divisor]);
        path = path.Append(nibbles[divisor..]);

        string asHex = path.Path.Bytes.ToHexString();
        asHex.Should().Be("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }
}
