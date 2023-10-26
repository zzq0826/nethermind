// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Evm;

public class TransactionSubstate
{
    private static readonly List<Address> _emptyDestroyList = new(0);
    private static readonly List<LogEntry> _emptyLogs = new(0);

    private const string SomeError = "error";
    private const string Revert = "revert";

    private const string RevertedErrorMessagePrefix = "Reverted ";


    public bool IsError => Error is not null && !ShouldRevert;
    public string? Error { get; }
    public ReadOnlyMemory<byte> Output { get; }
    public bool ShouldRevert { get; }
    public long Refund { get; }
    public IReadOnlyCollection<LogEntry> Logs { get; }
    public IReadOnlyCollection<Address> DestroyList { get; }

    public TransactionSubstate(EvmExceptionType exceptionType, bool isTracerConnected)
    {
        Error = isTracerConnected ? exceptionType.ToString() : SomeError;
        Refund = 0;
        DestroyList = _emptyDestroyList;
        Logs = _emptyLogs;
        ShouldRevert = false;
    }

    public TransactionSubstate(
        ReadOnlyMemory<byte> output,
        long refund,
        IReadOnlyCollection<Address> destroyList,
        IReadOnlyCollection<LogEntry> logs,
        bool shouldRevert,
        bool isTracerConnected)
    {
        Output = output;
        Refund = refund;
        DestroyList = destroyList;
        Logs = logs;
        ShouldRevert = shouldRevert;

        if (!ShouldRevert)
        {
            Error = null;
            return;
        }

        Error = Revert;

        if (!isTracerConnected)
            return;

        if (Output.Length <= 0)
            return;

        Error = string.Concat(
            RevertedErrorMessagePrefix,
            AbiEncoder.Instance.UnpackRevert(Output)
        );
    }
}
