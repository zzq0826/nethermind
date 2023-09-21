// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Verkle.Tree.Utils.EliasFano;

public class DArrayDecoder: IRlpStreamDecoder<DArray>
{
    private readonly DArrayIndexDecoder _indexDecoder = new DArrayIndexDecoder();
    private readonly BitVectorDecoder _bitVecDecoder = new BitVectorDecoder();

    public int GetLength(DArray item, RlpBehaviors rlpBehaviors)
    {
        return Rlp.LengthOfSequence(GetContentLength(item, rlpBehaviors));
    }

    public int GetContentLength(DArray item, RlpBehaviors rlpBehaviors)
    {
        int length = 0;
        length += _bitVecDecoder.GetLength(item.Data, rlpBehaviors);
        length += _indexDecoder.GetLength(item.IndexS0, rlpBehaviors);
        length += _indexDecoder.GetLength(item.IndexS1, rlpBehaviors);
        return length;
    }

    public DArray Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        rlpStream.ReadSequenceLength();
        return new DArray(
            _bitVecDecoder.Decode(rlpStream),
            _indexDecoder.Decode(rlpStream),
            _indexDecoder.Decode(rlpStream)
        );
    }

    public void Encode(RlpStream stream, DArray item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        int contentLength = GetContentLength(item, rlpBehaviors);
        stream.StartSequence(contentLength);
        _bitVecDecoder.Encode(stream, item.Data);
        _indexDecoder.Encode(stream, item.IndexS0);
        _indexDecoder.Encode(stream, item.IndexS1);
    }
}
