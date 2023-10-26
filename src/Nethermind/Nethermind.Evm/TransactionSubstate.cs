// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
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

    private const int RevertPrefix = 4;

    private const string RevertedErrorMessagePrefix = "Reverted ";
    private readonly ReadOnlyMemory<byte> ErrorFunctionSelector = ValueKeccak.Compute("Error(string)").Bytes[..RevertPrefix].ToArray();
    private readonly ReadOnlyMemory<byte> PanicFunctionSelector = ValueKeccak.Compute("Panic(uint256)").Bytes[..RevertPrefix].ToArray();

    private readonly IDictionary<UInt256, string> PanicReasons = new Dictionary<UInt256, string>
    {
        { 0x00, "generic panic" },
        { 0x01, "assert(false)" },
        { 0x11, "arithmetic underflow or overflow" },
        { 0x12, "division or modulo by zero" },
        { 0x21, "enum overflow" },
        { 0x22, "invalid encoded storage byte array accessed" },
        { 0x31, "out-of-bounds array access; popping on an empty array" },
        { 0x32, "out-of-bounds access of an array or bytesN" },
        { 0x41, "out of memory" },
        { 0x51, "uninitialized function" },
    };

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

        ReadOnlySpan<byte> span = Output.Span;
        Error = string.Concat(
            RevertedErrorMessagePrefix,
            TryGetErrorMessage(span) ?? DefaultErrorMessage(span)
        );
    }

    private string DefaultErrorMessage(ReadOnlySpan<byte> span) => span.ToHexString(true);

    private string? TryGetErrorMessage(ReadOnlySpan<byte> span)
    {
        const int wordSize = EvmPooledMemory.WordSize;

        if (span.Length < wordSize * 2) return null;
        if (!span.TrySplit(RevertPrefix, out ReadOnlySpan<byte> prefix)) return null;

        if (prefix.SequenceEqual(PanicFunctionSelector.Span))
        {
            if (!span.TrySplit(wordSize, out ReadOnlySpan<byte> panicCodeSpan)) return null;

            UInt256 panicCode = new(panicCodeSpan, isBigEndian: true);
            return !PanicReasons.TryGetValue(panicCode, out string panicReason)
                ? $"unknown panic code ({panicCode.ToHexString(skipLeadingZeros: true)})"
                : panicReason;
        }

        int start = (int)new UInt256(span.Slice(wordSize), isBigEndian: true);
        bool errorFunction = prefix.SequenceEqual(ErrorFunctionSelector.Span);
        if (!errorFunction || start == wordSize)
        {
            try
            {
                int lengthStart = checked(start + wordSize);
                if (lengthStart <= span.Length)
                {
                    int length = (int)new UInt256(span.Slice(start, wordSize), isBigEndian: true);
                    int size = checked(lengthStart + length);
                    if (size == span.Length)
                    {
                        ReadOnlySpan<byte> messageSpan = span.Slice(lengthStart, length);
                        return errorFunction
                            ? System.Text.Encoding.UTF8.GetString(messageSpan)
                            : messageSpan.ToHexString(true);
                    }
                }
            }
            catch (OverflowException) { }
        }

        return null;
    }
}
