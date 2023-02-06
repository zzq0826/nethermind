// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Extensions;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Proofs;
using NUnit.Framework;

namespace Nethermind.Verkle.Test;

public class VerkleProofTest
{
    [Test]
    public void TestVerkleProof()
    {
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(DbMode.MemDb);

        tree.Insert(VerkleTestUtils._keyVersion, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyBalance, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyNonce, VerkleTestUtils._emptyArray);
        tree.Insert(VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._valueEmptyCodeHashValue);
        tree.Insert(VerkleTestUtils._keyCodeSize, VerkleTestUtils._emptyArray);
        tree.Flush(0);

        VerkleProver prover = new VerkleProver(tree._stateDb);
        prover.CreateVerkleProof(new List<byte[]>(new[]
            {VerkleTestUtils._keyVersion, VerkleTestUtils._keyNonce, VerkleTestUtils._keyBalance, VerkleTestUtils._keyCodeCommitment}), out var root);

    }

    [Test]
    public void BasicProofTrue()
    {
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(DbMode.MemDb);

        byte[][] keys =
        {
            new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new byte[]{1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new byte[]{2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
            new byte[]{3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}
        };

        tree.Insert(keys[0], keys[0]);
        tree.Insert(keys[1], keys[1]);
        tree.Insert(keys[2], keys[2]);
        tree.Insert(keys[3], keys[3]);
        tree.Flush(0);

        VerkleProver prover = new VerkleProver(tree._stateDb);
        VerkleProof proof = prover.CreateVerkleProof(new List<byte[]>(keys), out Banderwagon root);
        // Console.WriteLine(proof.ToString());
        (bool, UpdateHint?) verified = Verifier.VerifyVerkleProof(proof, new List<byte[]>(keys), new List<byte[]?>(keys), root);
        Console.WriteLine(verified.Item1);
    }

    [Test]
    public void BasicProofTrueGoLang()
    {

        byte[] zeroKeyTest = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000000000");
        byte[] oneKeyTest = Bytes.FromHexString("0x0000000000000000000000000000000000000000000000000000000000000001");
        byte[] splitKeyTest = Bytes.FromHexString("0x0000000000720000000000000000000000000000000000000000000000000000");
        byte[] fourtyKeyTest = Bytes.FromHexString("0x4000000000000000000000000000000000000000000000000000000000000000");
        byte[] ffx32KeyTest = Bytes.FromHexString("0xffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff");
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(DbMode.MemDb);

        tree.Insert(zeroKeyTest, zeroKeyTest);
        tree.Insert(oneKeyTest, zeroKeyTest);
        tree.Insert(ffx32KeyTest, zeroKeyTest);
        tree.Flush(0);

        VerkleProver prover = new VerkleProver(tree._stateDb);
        prover.CreateVerkleProof(new List<byte[]>(new[]
        {
            ffx32KeyTest
        }), out var root);

    }
}
