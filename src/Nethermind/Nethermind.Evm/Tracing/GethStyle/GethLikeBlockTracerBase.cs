// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Evm.Tracing.GethStyle;

public abstract class GethLikeBlockTracerBase<TTracer> : BlockTracerBase<GethLikeTxTrace, TTracer>
    where TTracer : GethLikeTxTracer
{
    protected readonly GethTraceOptions _options;
    public GethLikeBlockTracerBase(GethTraceOptions options) : base(options.TxHash) => _options = options;

    protected override GethLikeTxTrace OnEnd(TTracer txTracer) => txTracer.BuildResult();
}
