// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm.Tracing.ParityStyle;

namespace Nethermind.JsonRpc.TraceStore;

public interface ITraceSerializer<TTrace>
{
    List<TTrace>? Deserialize(Span<byte> serialized);
    byte[] Serialize(IReadOnlyCollection<TTrace> traces);
}
