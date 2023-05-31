// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle;

namespace Nethermind.Evm.Tracing.DebugTrace
{
    public class DebugBlockTracer : BlockTracerBase<GethLikeTxTrace, DebugTxTracer>
    {

        public DebugBlockTracer(Keccak txHash)
            : base(txHash)
        {
        }

        protected override DebugTxTracer OnStart(Transaction? tx) => new(new GethLikeTxTracer(GethTraceOptions.Default))
        {
            IsStepByStepModeOn = true
        };

        protected override GethLikeTxTrace OnEnd(DebugTxTracer txTracer) => (txTracer.InnerTracer as GethLikeTxTracer).BuildResult();
        public bool IsBlocking => CurrentTxTracer.CanReadState;
        public void MoveNext() => CurrentTxTracer.MoveNext();
    }
}
