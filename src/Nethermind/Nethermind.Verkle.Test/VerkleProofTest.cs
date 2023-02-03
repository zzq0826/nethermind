// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
            {VerkleTestUtils._keyVersion, VerkleTestUtils._keyNonce, VerkleTestUtils._keyBalance, VerkleTestUtils._keyCodeCommitment}));

    }
}
