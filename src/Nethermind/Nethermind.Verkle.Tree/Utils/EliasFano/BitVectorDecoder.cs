// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Verkle.Tree.Utils.EliasFano;

public class BitVectorDecoder: IRlpStreamDecoder<BitVector>
{
    public int GetLength(BitVector item, RlpBehaviors rlpBehaviors)
    {
        return GetContentLength(item, rlpBehaviors);
    }
    public int GetContentLength(BitVector item, RlpBehaviors rlpBehaviors)
    {
        int length = 0;
        length += Rlp.LengthOf(item.Length);
        length += Rlp.LengthOf(MemoryMarshal.Cast<UIntPtr, byte>(CollectionsMarshal.AsSpan(item.Words)));
        return length;
    }

    public BitVector Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        int bitVecLength = rlpStream.DecodeInt();
        List<UIntPtr> bitVecWords =
            MemoryMarshal.Cast<byte, UIntPtr>(rlpStream.DecodeByteArraySpan()).ToArray().ToList();
        return new BitVector(bitVecWords, bitVecLength);
    }

    public void Encode(RlpStream stream, BitVector item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        stream.StartSequence(GetContentLength(item, rlpBehaviors));
        stream.Encode(item.Length);
        stream.Encode(MemoryMarshal.Cast<UIntPtr, byte>(CollectionsMarshal.AsSpan(item.Words)));
    }
}
