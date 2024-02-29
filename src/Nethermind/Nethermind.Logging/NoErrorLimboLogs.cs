// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;

namespace Nethermind.Logging
{
    /// <summary>
    /// Same as <see cref="LimboLogs"/> but throw on error logs.
    /// </summary>
    public class NoErrorLimboLogs : ILogManager
    {
        private NoErrorLimboLogs()
        {
        }

        private static NoErrorLimboLogs _instance;

        public static NoErrorLimboLogs Instance => _instance ?? LazyInitializer.EnsureInitialized(ref _instance, () => new NoErrorLimboLogs());

        public ILogger GetClassLogger(Type type) => LimboNoErrorLogger.Instance;

        public ILogger GetClassLogger<T>() => LimboNoErrorLogger.Instance;

        public ILogger GetClassLogger() => LimboNoErrorLogger.Instance;

        public ILogger GetLogger(string loggerName) => LimboNoErrorLogger.Instance;
    }
}
