// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Verkle.Tree.Utils.EliasFano;

public class DArrayIndexDecoder: IRlpStreamDecoder<DArrayIndex>
{
    public int GetLength(DArrayIndex item, RlpBehaviors rlpBehaviors)
    {
        return Rlp.LengthOfSequence(GetContentLength(item, rlpBehaviors));
    }

    public int GetContentLength(DArrayIndex item, RlpBehaviors rlpBehaviors)
    {
        int length = 0;
        length += Rlp.LengthOf(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.CurBlockPositions)));
        length += Rlp.LengthOf(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.BlockInventory)));
        length += Rlp.LengthOf(MemoryMarshal.Cast<ushort, byte>(CollectionsMarshal.AsSpan(item.SubBlockInventory)));
        length += Rlp.LengthOf(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.OverflowPositions)));
        length += Rlp.LengthOf(item.NumPositions);
        length += Rlp.LengthOf(item.OverOne);
        return length;
    }

    public DArrayIndex Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        return new DArrayIndex(
            MemoryMarshal.Cast<byte, int>(rlpStream.DecodeByteArraySpan()).ToArray(),
            MemoryMarshal.Cast<byte, int>(rlpStream.DecodeByteArraySpan()).ToArray(),
            MemoryMarshal.Cast<byte, ushort>(rlpStream.DecodeByteArraySpan()).ToArray(),
            MemoryMarshal.Cast<byte, int>(rlpStream.DecodeByteArraySpan()).ToArray(),
            rlpStream.DecodeInt(),
            rlpStream.DecodeBool()
        );
    }

    public void Encode(RlpStream stream, DArrayIndex item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int contentLength = GetContentLength(item, rlpBehaviors);
        stream.StartSequence(contentLength);
        stream.Encode(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.CurBlockPositions)));
        stream.Encode(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.BlockInventory)));
        stream.Encode(MemoryMarshal.Cast<ushort, byte>(CollectionsMarshal.AsSpan(item.SubBlockInventory)));
        stream.Encode(MemoryMarshal.Cast<int, byte>(CollectionsMarshal.AsSpan(item.OverflowPositions)));
        stream.Encode(item.NumPositions);
        stream.Encode(item.OverOne);
    }
}
