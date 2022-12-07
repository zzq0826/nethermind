// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers;
using System.Runtime.InteropServices;
using DotNetty.Buffers;
using DotNetty.Common.Utilities;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.Network;
using Nethermind.Network.P2P;
using Nethermind.Serialization.Rlp;

namespace Nethermind.JsonRpc.TraceStore;

public class ParityTxTraceFromStoreSerializer : ITraceSerializer<ParityTxTraceFromStore>
{
    private static readonly ParityTxTraceFromStoreDecoder _decoder = new();

    public List<ParityTxTraceFromStore> Deserialize(Span<byte> serialized)
    {
        Rlp.ValueDecoderContext valueDecoderContext = new(serialized);
        return Rlp.DecodeList(valueDecoderContext, _decoder);
    }

    public byte[] Serialize(IReadOnlyCollection<ParityTxTraceFromStore> traces)
    {
        int length = traces.Sum(i => _decoder.GetLength(i, RlpBehaviors.None));
        IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(length + Rlp.LengthOfSequence(length));
        try
        {
            NettyRlpStream stream = new(buffer);
            Rlp.EncodeArray(stream, traces, _decoder);
            return buffer.ReadAllBytes();
        }
        finally
        {
            buffer.SafeRelease();
        }
    }
}
