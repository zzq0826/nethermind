// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nethermind.Core.Threading;

public class ThreadPoolExecutor<TRequest, TResponse> : IDisposable
{
    private readonly Func<TRequest, TResponse> _func;
    private readonly BlockingCollection<(TRequest, TaskCompletionSource<TResponse>)> _tasks;
    private readonly CancellationTokenSource _innerSource;
    private readonly CancellationToken _cancellationToken;
    private readonly List<Thread> _threads;

    public ThreadPoolExecutor(Func<TRequest, TResponse> func, int threadCount, CancellationToken? cancellationToken = null)
    {
        _func = func;
        _tasks = new BlockingCollection<(TRequest, TaskCompletionSource<TResponse>)>(1);

        _innerSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken ?? CancellationToken.None);
        _cancellationToken = _innerSource.Token;

        _threads = Enumerable.Range(0, threadCount).Select(StartWorker).ToList();
    }

    private Thread StartWorker(int idx)
    {
        Thread thread = new(() =>
        {
            try
            {
                foreach ((TRequest? request, TaskCompletionSource<TResponse>? responseSource) in _tasks
                             .GetConsumingEnumerable(_cancellationToken))
                {
                    try
                    {
                        TResponse response = _func.Invoke(request);
                        responseSource.SetResult(response);
                    }
                    catch (OperationCanceledException e)
                    {
                        responseSource.SetCanceled(e.CancellationToken);
                    }
                    catch (Exception e)
                    {
                        responseSource.SetException(e);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        });
        thread.Start();
        return thread;
    }

    public Task<TResponse> Execute(TRequest request)
    {
        TaskCompletionSource<TResponse> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _tasks.Add((request, tcs), _cancellationToken);
        return tcs.Task;
    }

    public void Dispose()
    {
        _innerSource.Cancel();
        _innerSource.Dispose();

        // Any tasks not dequeued yet, need to be cancelled
        foreach ((TRequest, TaskCompletionSource<TResponse>) task in _tasks)
        {
            task.Item2.SetCanceled(_cancellationToken);
        }

        // Wait for all thread to exit
        foreach (Thread thread in _threads)
        {
            thread.Join();
        }
    }
}
