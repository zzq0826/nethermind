// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Nethermind.Evm.Tracing.DebugTrace;
using Nethermind.Evm.Tracing.GethStyle;

namespace Nethermind.Evm.Lab.Interfaces;
public static class Extensions
{
    public static GethTxTraceEntry GetCurrentEntry(this DebugTracer tracer)
    {
        FieldInfo _traceEntry = typeof(GethLikeTxTracer).GetField("_traceEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_traceEntry == null)
        {
            return default;
        }
        return (GethTxTraceEntry)_traceEntry.GetValue(tracer.InnerTracer);
    }
}
