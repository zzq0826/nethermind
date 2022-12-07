// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm.Tracing.ParityStyle;
using Nethermind.JsonRpc.Modules.Trace;

namespace Nethermind.JsonRpc.TraceStore;

public class ParityTxTraceFromStoreSerializerAdapter : ITraceSerializer<ParityLikeTxTrace>
{
    private readonly ITraceSerializer<ParityTxTraceFromStore> _serializer;

    public ParityTxTraceFromStoreSerializerAdapter(ITraceSerializer<ParityTxTraceFromStore> serializer)
    {
        _serializer = serializer;
    }

    public List<ParityLikeTxTrace>? Deserialize(Span<byte> serialized)
    {
        throw new NotSupportedException();
    }

    public byte[] Serialize(IReadOnlyCollection<ParityLikeTxTrace> traces)
    {
        ParityTxTraceFromStore[] parityTxTraceFromStores = ParityTxTraceFromStore.FromTxTrace(traces).ToArray();
        return _serializer.Serialize(parityTxTraceFromStores);
    }
}
