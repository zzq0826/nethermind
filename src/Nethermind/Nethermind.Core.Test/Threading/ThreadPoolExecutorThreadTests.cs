// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Core.Threading;
using NUnit.Framework;

namespace Nethermind.Core.Test.Threading;

public class ThreadPoolExecutorThreadTests
{

    [Test]
    public async Task CanExecuteTasks()
    {
        using ThreadPoolExecutor<int, int> executor = new((input) => input + 1, 1);

        Task<int> t1 = executor.Execute(1);
        Task<int> t2 = executor.Execute(2);
        Task<int> t3 = executor.Execute(3);
        Task<int> t4 = executor.Execute(4);

        (await t1).Should().Be(2);
        (await t2).Should().Be(3);
        (await t3).Should().Be(4);
        (await t4).Should().Be(5);
    }

    [Test]
    public async Task ExecuteTasksInParallel()
    {
        ManualResetEvent resetEvent = new ManualResetEvent(false);
        int threadCount = 0;

        using ThreadPoolExecutor<int, int> executor = new((input) =>
        {
            threadCount++;
            if (threadCount == 4)
            {
                resetEvent.Set();
            }
            resetEvent.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
            return input + 1;
        }, 4);

        await Task.WhenAll(
            executor.Execute(1),
            executor.Execute(2),
            executor.Execute(3),
            executor.Execute(4)
        );
    }

    [Test]
    public async Task BlocksTasksCreationWhenQueueFull()
    {
        ManualResetEvent resetEvent = new ManualResetEvent(false);

        using ThreadPoolExecutor<int, int> executor = new((input) =>
        {
            resetEvent.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
            return input + 1;
        }, 1);

        Task<int> t1 = executor.Execute(1);
        Task<int> t2 = executor.Execute(2);

        Task<int> t3 = null;
        Task.Run(() =>
        {
            t3 = executor.Execute(3);
        });

        await Task.Delay(100);
        t3.Should().BeNull();
        resetEvent.Set();
        await Task.Delay(100);
        t3.Should().NotBeNull();

        (await t1).Should().Be(2);
        (await t2).Should().Be(3);
        (await t3).Should().Be(4);
    }

    [Test]
    public async Task ShouldForwardException()
    {
        using ThreadPoolExecutor<int, int> executor = new((input) =>
        {
            throw new Exception("testException");
        }, 1);

        try
        {
            await executor.Execute(1);
        }
        catch (Exception e)
        {
            e.Message.Should().Be("testException");
            return;
        }

        Assert.Fail("Should throw");
    }

    [Test]
    public async Task ShouldForwardCancellation()
    {
        using ThreadPoolExecutor<int, int> executor = new((input) =>
        {
            throw new OperationCanceledException();
        }, 1);

        Task<int> t = executor.Execute(1);
        try
        {
            await t;
        }
        catch (Exception)
        {
        }
        t.IsCanceled.Should().BeTrue();
    }

    [Test]
    public async Task ShouldCancelPendingTasksOnDispose()
    {
        ManualResetEvent resetEvent = new ManualResetEvent(false);

        ThreadPoolExecutor<int, int> executor = new((input) =>
        {
            resetEvent.WaitOne(TimeSpan.FromSeconds(10)).Should().BeTrue();
            return input + 1;
        }, 1);

        Task<int> t1 = executor.Execute(1);
        Task<int> t2 = executor.Execute(2);

        Task.Run(() =>
        {
            resetEvent.Set();
        });

        executor.Dispose();

        try
        {
            await t1;
        }
        catch (Exception)
        {
        }

        try
        {
            await t2;
        }
        catch (Exception)
        {
        }

        t2.IsCanceled.Should().BeTrue();
    }
}
