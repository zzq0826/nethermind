// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Nethermind.Evm.Tracing.ParityStyle;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.Logging;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Nethermind.JsonRpc.TraceStore.Tests;

public class TraceSerializerTests
{
    [Test]
    public void can_deserialize_deep_graph()
    {
        List<ParityLikeTxTrace>? traces = Deserialize(new ParityLikeTraceSerializer(LimboLogs.Instance));
        traces?.Count.Should().Be(36);
    }

    [Test]
    public void cant_deserialize_deep_graph()
    {
        Func<List<ParityLikeTxTrace>?> traces = () => Deserialize(new ParityLikeTraceSerializer(LimboLogs.Instance, 128));
        traces.Should().Throw<JsonReaderException>();
    }

    private List<ParityLikeTxTrace>? Deserialize(ParityLikeTraceSerializer serializer)
    {
        Type type = GetType();
        using Stream stream = type.Assembly.GetManifestResourceStream(type.Namespace + ".xdai-17600039.json")!;
        List<ParityLikeTxTrace>? traces = serializer.Deserialize(stream);
        return traces;
    }

    [Test]
    public void rlp_roundtrip()
    {
        List<ParityLikeTxTrace> traces = Deserialize(new ParityLikeTraceSerializer(LimboLogs.Instance))!;
        ParityTxTraceFromStore[] tracesConverted = traces.SelectMany(ParityTxTraceFromStore.FromTxTrace).ToArray();
            // new[] { traces.SelectMany(ParityTxTraceFromStore.FromTxTrace).First() };
        ParityTxTraceFromStoreSerializer rlpSerializer = new();
        byte[] bytes = rlpSerializer.Serialize(tracesConverted);
        List<ParityTxTraceFromStore> tracesDeserialized = rlpSerializer.Deserialize(bytes);
        tracesDeserialized.Should().BeEquivalentTo(tracesConverted);
    }
}
