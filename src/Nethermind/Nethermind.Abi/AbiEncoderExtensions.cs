// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Abi
{
    public static class AbiEncoderExtensions
    {
        public static byte[] Encode(this IAbiEncoder encoder, AbiEncodingInfo abiEncodingInfo, params object[] arguments)
            => encoder.Encode(abiEncodingInfo.EncodingStyle, abiEncodingInfo.Signature, arguments);

        public static object[] Decode(this IAbiEncoder encoder, AbiEncodingInfo abiEncodingInfo, byte[] data)
            => encoder.Decode(abiEncodingInfo.EncodingStyle, abiEncodingInfo.Signature, data);

        private static readonly IDictionary<UInt256, string> _panicReasons = new Dictionary<UInt256, string>
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

        private const string RevertedErrorMessagePrefix = "Reverted ";

        public static string UnpackRevert(this IAbiEncoder encoder, ReadOnlyMemory<byte> data)
        {
            static bool TryDecode<T>(IAbiEncoder encoder, ReadOnlyMemory<byte> data, AbiSignature signature, [NotNullWhen(true)] out T? result)
            {
                ReadOnlySpan<byte> address = signature.Address;
                if (data.Span.StartsWith(address))
                {
                    result = (T)encoder.Decode(AbiEncodingStyle.IncludeSignature, signature, data)[0]!;
                    return true;
                }

                result = default;
                return false;
            }


            if (TryDecode(encoder, data, AbiSignature.Revert, out string? result))
            {
                return result;
            }

            if (TryDecode(encoder, data, AbiSignature.Panic, out UInt256 panicCode))
            {
                return !_panicReasons.TryGetValue(panicCode, out string? panicReason)
                    ? $"unknown panic code ({panicCode.ToHexString(skipLeadingZeros: true)})"
                    : panicReason;
            }

            throw new AbiException("invalid data for unpacking");
        }


    }
}
