// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Evm.Tracing.GethStyle.Native;

public class NoopBlockTracer : GethLikeBlockTracerBase<NoopTxTracer>
{
    public NoopBlockTracer(GethTraceOptions options) : base(options) { }
    protected override NoopTxTracer OnStart(Transaction? tx) => new(_options);
}

public sealed class NoopTxTracer : GethLikeTxTracer
{
    public NoopTxTracer(GethTraceOptions options) : base(options)
    {
        IsTracingRefunds = true;
        IsTracingActions = true;
        IsTracingMemory = true;
        IsTracingStack = true;
    }

    public override GethLikeTxTrace BuildResult()
    {
        GethLikeTxTrace result = base.BuildResult();
        result.CustomTracerResult = new GethLikeJavascriptTrace { Value = new { } };
        return result;
    }
}
