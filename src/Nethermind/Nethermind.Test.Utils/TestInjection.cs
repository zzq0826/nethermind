// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Test.Utils;

//An example class to inject into constructors a counter
public static class TestInjection
{
    //A variable to keep instantiations count
    public static int _counter;

    //A function that adds to a counter on call
    public static void AddCounter()
    {
        Interlocked.Increment(ref _counter);
    }
}
