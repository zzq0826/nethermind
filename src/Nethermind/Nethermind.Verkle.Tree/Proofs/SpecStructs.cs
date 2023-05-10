// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Proofs;

namespace Nethermind.Verkle.Tree.Proofs;

public struct VProof
{
    public VerkleProofStruct MultiPoint { get; set; }
    public List<byte> ExtStatus { get; set; }
    public List<Banderwagon> Cs { get; set; }
    public List<byte[]> PoaStems { get; set; }
    public List<byte[]> Keys { get; set; }
    public List<byte[]> Values { get; set; }
}

public struct ExecutionWitness
{
    public StateDiff StateDiff { get; set; }
    public VProof Proof { get; set; }
}

public struct SuffixStateDiff
{
    public byte Suffix { get; set; }

    // byte32
    public byte[]? CurrentValue { get; set; }

    // byte32
    public byte[]? NewValue { get; set; }
}

public struct StemStateDiff
{
    // byte31
    public byte[] Stem { get; set; }

    // max length = 256
    public List<SuffixStateDiff> SuffixDiffs { get; set; }
}

public struct StateDiff
{
    // max length = 2**16
    public List<StemStateDiff> Diff { get; set; }
}
