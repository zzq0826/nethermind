// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Proofs;

namespace Nethermind.Verkle;

public struct VProof
{
    public MultiProofStruct _multiPoint;
    public List<byte> _extStatus;
    public List<Banderwagon> _cS;
    public List<byte[]> _poaStems;
    public List<byte[]> _keys;
    public List<byte[]> _values;
}

public class VerkleProof
{

}
