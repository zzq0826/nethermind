// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using DotNetty.Buffers;
using Nethermind.Core.Crypto;

namespace Nethermind.Network.P2P.Subprotocols.Eth.V63.Messages
{
    public class GetNodeDataMessageSerializer : ValueHashesMessageSerializer<GetNodeDataMessage>
    {
        public override GetNodeDataMessage Deserialize(IByteBuffer byteBuffer)
        {
            ValueKeccak[] keys = DeserializeHashes(byteBuffer);
            return new GetNodeDataMessage(keys);
        }
    }
}
