// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Test.Utils;

[TestFixture]
public class TestInsertionFactory
{
    [Test]
    public void Test()
    {
        //Make sure static variable is set to 0 and InsertionFactory is cleared from previous mods
        TestInjection._counter = 0;
        InsertionFactory.Clear();

        // Inject code into the constructor
        InsertionFactory.ModifyTypeConstructor<Transaction>(typeof(TestInjection), nameof(TestInjection.AddCounter));

        //See that static counter is instantiated to 0
        Assert.AreEqual(0, TestInjection._counter);

        var test1 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(1, TestInjection._counter);

        var test2 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(2, TestInjection._counter);

        //See that static counter has grown to be 3
        var test3 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(3, TestInjection._counter);
    }

    [Test]
    public void TestMultipleInjections()
    {
        //Make sure static variable is set to 0 and InsertionFactory is cleared from previous mods
        TestInjection._counter = 0;
        InsertionFactory.Clear();

        // Inject code into the constructor (2 times with same call would do +2)
        InsertionFactory.ModifyTypeConstructor<Transaction>(typeof(TestInjection), nameof(TestInjection.AddCounter));
        InsertionFactory.ModifyTypeConstructor<Transaction>(typeof(TestInjection), nameof(TestInjection.AddCounter));

        //See that static counter is instantiated to 0
        Assert.AreEqual(0, TestInjection._counter);

        var test1 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(2, TestInjection._counter);

        var test2 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(4, TestInjection._counter);

        //See that static counter has grown to be 6
        var test3 = InsertionFactory.Get<Transaction>();
        Assert.AreEqual(6, TestInjection._counter);
    }
}
