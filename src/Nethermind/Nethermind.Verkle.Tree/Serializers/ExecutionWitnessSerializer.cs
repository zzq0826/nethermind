// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Verkle;
using Nethermind.Serialization.Rlp;
using Nethermind.Verkle.Tree.Proofs;

namespace Nethermind.Verkle.Tree.Serializers;

public class ExecutionWitnessSerializer: IRlpStreamDecoder<ExecutionWitness>, IRlpObjectDecoder<ExecutionWitness>
{
    public int GetLength(ExecutionWitness item, RlpBehaviors rlpBehaviors)
    {
        throw new NotImplementedException();
    }

    public ExecutionWitness Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }

    public void Encode(RlpStream stream, ExecutionWitness item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }

    public Rlp Encode(ExecutionWitness item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }
}

public class StateDiffSerializer : IRlpStreamDecoder<StateDiff>, IRlpObjectDecoder<StateDiff>
{
    private int GetLengthOfSuffixDiff(SuffixStateDiff item)
    {
        int contentLength = Rlp.LengthOf(item.Suffix);
        contentLength += Rlp.LengthOf(item.CurrentValue);
        contentLength += Rlp.LengthOf(item.NewValue);
        return contentLength;
    }

    private int GetLengthOfStemStateDiff(StemStateDiff item)
    {
        int contentLength = 0;
        contentLength += item.Stem.Bytes.Length;
        int suffixStateDiffLength = 0;
        foreach (SuffixStateDiff suffixStateDiff in item.SuffixDiffs)
        {
            suffixStateDiffLength = Rlp.LengthOfSequence(GetLengthOfSuffixDiff(suffixStateDiff));
        }
        contentLength += Rlp.LengthOfSequence(suffixStateDiffLength);
        return contentLength;
    }

    private int GetContentLength(StateDiff item, RlpBehaviors rlpBehaviors)
    {
        int stateDiffLength = 0;
        foreach (StemStateDiff stemStateDiff in item.SuffixDiffs)
        {
            stateDiffLength += Rlp.LengthOfSequence(GetLengthOfStemStateDiff(stemStateDiff));
        }
        return stateDiffLength;
    }
    public int GetLength(StateDiff item, RlpBehaviors rlpBehaviors)
    {
        return Rlp.LengthOfSequence(GetContentLength(item, rlpBehaviors));
    }

    public StateDiff Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }

    public void Encode(RlpStream stream, StateDiff item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        var contentLength = GetContentLength(item, rlpBehaviors);
        stream.StartSequence(contentLength);
        foreach (StemStateDiff stemStateDiff in item.SuffixDiffs)
        {
            stream.StartSequence(GetLengthOfStemStateDiff(stemStateDiff));
            stream.Encode(stemStateDiff.Stem.Bytes);

            int suffixStateDiffLength = 0;
            foreach (SuffixStateDiff suffixStateDiff in stemStateDiff.SuffixDiffs)
            {
                suffixStateDiffLength = Rlp.LengthOfSequence(GetLengthOfSuffixDiff(suffixStateDiff));
            }

            stream.StartSequence(suffixStateDiffLength);
            foreach (SuffixStateDiff suffixStateDiff in stemStateDiff.SuffixDiffs)
            {
                stream.StartSequence(GetLengthOfSuffixDiff(suffixStateDiff));
                stream.Encode(suffixStateDiff.Suffix);

                if(suffixStateDiff.CurrentValue is null) stream.EncodeEmptyByteArray();
                else stream.Encode(suffixStateDiff.CurrentValue);

                if(suffixStateDiff.NewValue is null) stream.EncodeEmptyByteArray();
                else stream.Encode(suffixStateDiff.NewValue);
            }
        }
    }

    public Rlp Encode(StateDiff item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }
}

public class WitnessVerkleProofSerializer : IRlpStreamDecoder<WitnessVerkleProof>, IRlpObjectDecoder<WitnessVerkleProof>
{
    public int GetLength(WitnessVerkleProof item, RlpBehaviors rlpBehaviors)
    {
        throw new NotImplementedException();
    }

    public WitnessVerkleProof Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }

    public void Encode(RlpStream stream, WitnessVerkleProof item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }

    public Rlp Encode(WitnessVerkleProof item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        throw new NotImplementedException();
    }
}
