// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Microsoft.IdentityModel.Tokens;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.ParityStyle;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.Serialization.Rlp;

namespace Nethermind.JsonRpc.TraceStore;

public class ParityTxTraceFromStoreDecoder : IRlpStreamDecoder<ParityTxTraceFromStore>, IRlpValueDecoder<ParityTxTraceFromStore>
{
    private const char EmptyTransactionPosition = char.MaxValue;

    public int GetLength(ParityTxTraceFromStore item, RlpBehaviors rlpBehaviors)
    {
        return Rlp.LengthOf(item.Action.From) +
               Rlp.LengthOf(item.Action.To) +
               Rlp.LengthOf(item.Action.Value) +
               Rlp.LengthOf(item.Action.Author) +
               Rlp.LengthOf(item.Action.RewardType) +
               Rlp.LengthOf(item.Action.CallType) +
               Rlp.LengthOf(item.Action.CreationMethod) +
               Rlp.LengthOf(item.Action.Gas) +
               Rlp.LengthOf(item.Action.Input) +
               Rlp.LengthOf(item.Result?.Address) +
               Rlp.LengthOf(item.Result?.Code) +
               Rlp.LengthOf(item.Result?.Output) +
               Rlp.LengthOf(item.Result?.GasUsed ?? 0L) +
               Rlp.LengthOf(item.Subtraces) +
               Rlp.LengthOf(item.Type) +
               Rlp.LengthOf(item.TransactionPosition ?? EmptyTransactionPosition) +
               Rlp.LengthOf(item.TraceAddress.LastOrDefault()) +
               Rlp.LengthOf(item.Error);
    }

    public ParityTxTraceFromStore Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        static ParityTraceResult? GetResult(RlpStream stream)
        {
            ParityTraceResult result = new()
            {
                Address = stream.DecodeAddress(),
                Code = stream.DecodeByteArray(),
                Output = stream.DecodeByteArray(),
                GasUsed = stream.DecodeLong()
            };

            return result.Address is null && result.Code.IsNullOrEmpty() && result.Output.IsNullOrEmpty() && result.GasUsed == 0 ? null : result;
        }

        static int? GetTransactionPosition(RlpStream stream)
        {
            int transactionPosition = stream.DecodeInt();
            return transactionPosition == EmptyTransactionPosition ? null : transactionPosition;
        }

        return new()
        {
            Action = new()
            {
                From = rlpStream.DecodeAddress(),
                To = rlpStream.DecodeAddress(),
                Value = rlpStream.DecodeUInt256(),
                Author = rlpStream.DecodeAddress(),
                RewardType = rlpStream.DecodeString(),
                CallType = rlpStream.DecodeString(),
                CreationMethod = rlpStream.DecodeString(),
                Gas = rlpStream.DecodeLong(),
                Input = rlpStream.DecodeByteArray()
            },
            Result = GetResult(rlpStream),
            Subtraces = rlpStream.DecodeInt(),
            Type = rlpStream.DecodeString(),
            TransactionPosition = GetTransactionPosition(rlpStream),
            TraceAddress = new[] { rlpStream.DecodeInt() },
            Error = rlpStream.DecodeString(),
        };
    }

    public void Encode(RlpStream stream, ParityTxTraceFromStore item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.Encode(item.Action.From);
        stream.Encode(item.Action.To);
        stream.Encode(item.Action.Value);
        stream.Encode(item.Action.Author);
        stream.Encode(item.Action.RewardType);
        stream.Encode(item.Action.CallType);
        stream.Encode(item.Action.CreationMethod);
        stream.Encode(item.Action.Gas);
        stream.Encode(item.Action.Input);
        stream.Encode(item.Result?.Address);
        stream.Encode(item.Result?.Code);
        stream.Encode(item.Result?.Output);
        stream.Encode(item.Result?.GasUsed ?? 0L);
        stream.Encode(item.Subtraces);
        stream.Encode(item.Type);
        stream.Encode(item.TransactionPosition ?? EmptyTransactionPosition);
        stream.Encode(item.TraceAddress.LastOrDefault());
        stream.Encode(item.Error);
    }

    public ParityTxTraceFromStore Decode(ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        static ParityTraceResult? GetResult(Rlp.ValueDecoderContext decoderContext)
        {
            ParityTraceResult result = new()
            {
                Address = decoderContext.DecodeAddress(),
                Code = decoderContext.DecodeByteArray(),
                Output = decoderContext.DecodeByteArray(),
                GasUsed = decoderContext.DecodeLong()
            };

            return result.Address is null && result.Code.IsNullOrEmpty() && result.Output.IsNullOrEmpty() && result.GasUsed == 0 ? null : result;
        }

        static int? GetTransactionPosition(Rlp.ValueDecoderContext decoderContext)
        {
            int transactionPosition = decoderContext.DecodeInt();
            return transactionPosition == EmptyTransactionPosition ? null : transactionPosition;
        }

        return new()
        {
            Action = new()
            {
                From = decoderContext.DecodeAddress(),
                To = decoderContext.DecodeAddress(),
                Value = decoderContext.DecodeUInt256(),
                Author = decoderContext.DecodeAddress(),
                RewardType = decoderContext.DecodeString(),
                CallType = decoderContext.DecodeString(),
                CreationMethod = decoderContext.DecodeString(),
                Gas = decoderContext.DecodeLong(),
                Input = decoderContext.DecodeByteArray()
            },
            Result = GetResult(decoderContext),
            Subtraces = decoderContext.DecodeInt(),
            Type = decoderContext.DecodeString(),
            TransactionPosition = GetTransactionPosition(decoderContext),
            TraceAddress = new[] { decoderContext.DecodeInt() },
            Error = decoderContext.DecodeString(),
        };
    }
}
