// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
using Nethermind.Field.Montgomery.FrEElement;
using Nethermind.Verkle.Curve;

namespace Nethermind.Verkle.Proofs;

public struct VerkleProof
{
    public VerificationHint VerifyHint;
    public Banderwagon[] CommsSorted;
    public VerkleProofStruct Proof;

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append("\n####[Verkle Proof]####\n");
        stringBuilder.Append("\n###[Verify Hint]###\n");
        stringBuilder.Append(VerifyHint.ToString());
        stringBuilder.Append("\n###[Comms Sorted]###\n");
        foreach (Banderwagon comm in CommsSorted)
        {
            stringBuilder.AppendJoin(", ", comm.ToBytesLittleEndian().Reverse().ToArray());
            stringBuilder.Append('\n');
        }
        stringBuilder.Append("\n###[Inner Proof]###\n");
        stringBuilder.Append(Proof.ToString());
        return stringBuilder.ToString();
    }
}

public struct VerificationHint
{
    public byte[] Depths;
    public ExtPresent[] ExtensionPresent;
    public byte[][] DifferentStemNoProof;

    public override string ToString()
    {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append("\n##[Depths]##\n");
        stringBuilder.AppendJoin(", ", Depths);
        stringBuilder.Append("\n##[ExtensionPresent]##\n");
        stringBuilder.AppendJoin(", ", ExtensionPresent.Select(x => x.ToString()));
        stringBuilder.Append("\n##[DifferentStemNoProof]##\n");
        foreach (byte[] stem in DifferentStemNoProof)
        {
            stringBuilder.AppendJoin(", ", stem);
        }
        return stringBuilder.ToString();
    }
}

public struct UpdateHint
{
    public SortedDictionary<byte[], (ExtPresent, byte)> DepthAndExtByStem;
    public SortedDictionary<List<byte>, Banderwagon> CommByPath;
    public SortedDictionary<List<byte>, byte[]> DifferentStemNoProof;
}

public enum ExtPresent
{
    None,
    DifferentStem,
    Present
}

public struct SuffixPoly
{
    public FrE[] c1;
    public FrE[] c2;
}
