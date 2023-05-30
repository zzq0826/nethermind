// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using DotNetty.Buffers;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Network.P2P.Subprotocols.Verkle.Messages;

public class GetSubTreeRangeMessageSerializer: VerkleMessageSerializerBase<GetSubTreeRangeMessage>
{
    public override void Serialize(IByteBuffer byteBuffer, GetSubTreeRangeMessage message)
    {
        NettyRlpStream rlpStream = GetRlpStreamAndStartSequence(byteBuffer, message);

        rlpStream.Encode(message.RequestId);
        rlpStream.Encode(message.SubTreeRange.RootHash.Bytes);
        rlpStream.Encode(message.SubTreeRange.StartingStem);

        rlpStream.Encode(message.SubTreeRange.LimitStem ?? Keccak.MaxValue.Bytes);
        rlpStream.Encode(message.ResponseBytes == 0 ? 1000_000 : message.ResponseBytes);
    }

    protected override GetSubTreeRangeMessage Deserialize(RlpStream rlpStream)
    {
        GetSubTreeRangeMessage message = new();
        rlpStream.ReadSequenceLength();

        message.RequestId = rlpStream.DecodeLong();
        message.SubTreeRange = new(rlpStream.DecodeByteArray(), rlpStream.DecodeByteArray(), rlpStream.DecodeByteArray());
        message.ResponseBytes = rlpStream.DecodeLong();

        return message;
    }

    public override int GetLength(GetSubTreeRangeMessage message, out int contentLength)
    {
        contentLength = Rlp.LengthOf(message.RequestId);
        contentLength += Rlp.LengthOf(message.SubTreeRange.RootHash.Bytes);
        contentLength += Rlp.LengthOf(message.SubTreeRange.StartingStem);
        contentLength += Rlp.LengthOf(message.SubTreeRange.LimitStem);
        contentLength += Rlp.LengthOf(message.ResponseBytes);

        return Rlp.LengthOfSequence(contentLength);
    }
}
