// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Evm.Tracing.GethStyle.Native;

public class FourByteBlockTracer : GethLikeBlockTracerBase<FourByteTxTracer>
{
    public FourByteBlockTracer(GethTraceOptions options) : base(options) { }
    protected override FourByteTxTracer OnStart(Transaction? tx) => new(_options);
}

public sealed class FourByteTxTracer : GethLikeTxTracer
{
    private Dictionary<string, int> ids = new();
    private ReadOnlyMemory<byte> input;

    public FourByteTxTracer(GethTraceOptions options) : base(options)
    {
        IsTracingActions = true;
    }

    private void RegisterFourByteId(ReadOnlyMemory<byte> data, int length)
    {
        string key = Convert.ToHexString(data.ToArray()).ToLower() + "-" + length.ToString();
        if (ids.ContainsKey(key))
        {
            ids[key]++;
        }
        else
        {
            ids.Add(key, 1);
        }
    }

    public override void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        base.ReportAction(gas, value, from, to, input, callType, isPrecompileCall);

        if (callType == ExecutionType.TRANSACTION)
        {
            this.input = input;
        }
        else
        {
            // ENTER
            if (input.Length < 4)
            {
                return;
            }
            // primarily we want to avoid CREATE/CREATE2/SELFDESTRUCT
            if (callType != ExecutionType.DELEGATECALL && callType != ExecutionType.STATICCALL && callType != ExecutionType.CALL && callType != ExecutionType.CALLCODE)
            {
                return;
            }

            // Skip any pre-compile invocations, those are just fancy opcodes
            if (isPrecompileCall)
            {
                return;
            }

            RegisterFourByteId(input.Slice(0, 4), input.Length - 4);
        }
    }

    public override GethLikeTxTrace BuildResult()
    {
        if (input.Length >= 4)
        {
            RegisterFourByteId(input.Slice(0, 4), input.Length - 4);
        }

        GethLikeTxTrace result = base.BuildResult();
        result.CustomTracerResult = new GethLikeJavascriptTrace { Value = ids };
        return result;
    }
}
