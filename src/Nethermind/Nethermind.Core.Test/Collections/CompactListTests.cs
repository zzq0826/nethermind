// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using FluentAssertions;
using Nethermind.Core.Collections;
using NUnit.Framework;

namespace Nethermind.Core.Test.Collections;

public class CompactListTests
{
    private CompactList<int> OneThousandItems()
    {
        CompactList<int> list = new CompactList<int>(7);

        for (int i = 0; i < 1000; i++)
        {
            list.Add(i);
        }

        return list;
    }

    [Test]
    public void TestAddSequence()
    {
        IList<int> list = OneThousandItems();

        list.Count.Should().Be(1000);
        for (int i = 0; i < 1000; i++)
        {
            list[i].Should().Be(i);
        }

        int cidx = 0;
        foreach (int idx in list)
        {
            idx.Should().Be(cidx);
            cidx++;
        }
    }

    [Test]
    public void TestIterate()
    {
        IList<int> list = OneThousandItems();
        int cidx = 0;
        foreach (int idx in list)
        {
            idx.Should().Be(cidx);
            cidx++;
        }

        cidx.Should().Be(1000);
    }

    [Test]
    public void TestRemoveFromMiddle()
    {
        IList<int> list = OneThousandItems();
        for (int i = 0; i < 500; i++)
        {
            list.Remove(i);
        }

        for (int i = 0; i < 500; i++)
        {
            list[i].Should().Be(i+500);
        }
    }

    [Test]
    public void TestClear()
    {
        IList<int> list = OneThousandItems();
        list.Count.Should().Be(1000);
        list.Clear();
        list.Count.Should().Be(0);
    }

    [Test]
    public void TestBoundCheck()
    {
        IList<int> list = OneThousandItems();

        Func<int> act = () => list[-1];
        act.Should().Throw<IndexOutOfRangeException>();

        act = () => list[1000];
        act.Should().Throw<IndexOutOfRangeException>();

        act = () => list[1001];
        act.Should().Throw<IndexOutOfRangeException>();

        act = () => list[0];
        act.Should().NotThrow();
    }

    [Test]
    public void TestAddFromMiddle()
    {
        IList<int> list = OneThousandItems();

        list.Count.Should().Be(1000);
        for (int i = 0; i < 500; i++)
        {
            list.Insert(500, i);
        }

        for (int i = 0; i < 500; i++)
        {
            list[i].Should().Be(i);
            list[i+500].Should().Be(500-i-1);
            list[i+1000].Should().Be(i+500);
        }
    }

    [Test]
    public void RandomSort()
    {
        CompactList<int> list = new CompactList<int>(7);
        List<int> list2 = new List<int>();

        for (int i = 0; i < 1000; i++)
        {
            int num = Random.Shared.Next(1000);
            list.Add(num);
            list2.Add(num);
        }

        list.Sort();
        list2.Sort();

        for (int i = 0; i < 1000; i++)
        {
            list[i].Should().Be(list2[i]);
        }
    }

    [Test]
    public void RandomSortReverse()
    {
        CompactList<int> list = new CompactList<int>(7);
        List<int> list2 = new List<int>();

        for (int i = 0; i < 1000; i++)
        {
            int num = Random.Shared.Next(1000);
            list.Add(num);
            list2.Add(num);
        }

        list.Sort((n1, n2) => n1-n2);
        list2.Sort((n1, n2) => n1-n2);

        for (int i = 0; i < 1000; i++)
        {
            list[i].Should().Be(list2[i]);
        }
    }
}
