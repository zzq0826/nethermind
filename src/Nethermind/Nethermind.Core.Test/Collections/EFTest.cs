// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using FluentAssertions;
using Nethermind.Core.Collections;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Nethermind.Core.Test.Collections;

public class EFTest
{
    [Test]
    public void TestBuilder()
    {
        UIntPtr[] data = new UIntPtr[200];
        for (uint i = 0; i < 200; i++) data[i] = i * 20000;

        EliasFano efb = new (data[^1], data.Length);

        foreach (UIntPtr val in data) efb.Push(val);
        EliasFanoS ef = efb.Build();
        Console.WriteLine(string.Join(", ", data));
        Console.WriteLine(ef.Rank(300000));
    }

    [Test]
    public void TestCase()
    {
        EliasFano efb = new (8, 4);
        efb.Push(1);
        efb.Push(3);
        efb.Push(3);
        efb.Push(7);

        EliasFanoS ef = efb.Build();
        ef.Rank(3).Should().Be(1);
        ef.Rank(4).Should().Be(3);
        ef.Rank(8).Should().Be(4);
        Assert.Throws<ArgumentException>(() => ef.Rank(9));
    }

    [Test]
    public void TestDArrayIndexOverOne()
    {
        BitVector bv = BitVector.FromBit(false, 3);
        DArrayIndex da = new DArrayIndex(bv, true);
        Console.WriteLine(da.Select(bv, 0));
        Console.WriteLine(da.Select(bv, 1));
        Console.WriteLine(da.Select(bv, 2));
        Console.WriteLine(da.Select(bv, 3));
    }

    [Test]
    public void TestDArrayIndexOverZero()
    {
        BitVector bv = BitVector.FromBit(false, 3);
        DArrayIndex da = new DArrayIndex(bv, false);
        Console.WriteLine(da.Select(bv, 0));
        Console.WriteLine(da.Select(bv, 1));
        Console.WriteLine(da.Select(bv, 2));
        Console.WriteLine(da.Select(bv, 3));
    }

    [Test]
    public void TestDArrayAllZero()
    {
        DArray da = DArray.FromBits(new bool[] { false, false, false });
        Console.WriteLine(da.Select1(0));
    }

    [Test]
    public void TestDArrayRank0()
    {
        DArray da = DArray.FromBits(new bool[] { false, true, false });
        Console.WriteLine(da.Select1(0));
    }

    [Test]
    public void TestDArraySelect1()
    {
        DArray da = DArray.FromBits(new bool[] { false, true, false });
        Console.WriteLine(da.Select1(0));
    }
}
