// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.Tracing.GethStyle.Javascript;

namespace Nethermind.Evm.Tracing.GethStyle;

public class GethLikeBlockMemoryTracer : GethLikeBlockTracerBase<GethLikeTxMemoryTracer>
{
    public GethLikeBlockMemoryTracer(GethTraceOptions options) : base(options) { }

    protected override GethLikeTxMemoryTracer OnStart(Transaction? tx) => new(_options);

}
