// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Db.Rocks;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree.Proofs;

namespace Nethermind.Verkle.Tree.Test;

public class VerkleRangeProofTests
{
    [Test]
    public void TestSyncProofCreationAndVerification()
    {
        VerkleTree tree = VerkleTestUtils.GetVerkleTreeForTest(DbMode.MemDb);

        byte[][] stems =
        {
            new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2},
            new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
            new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3},
            new byte[]{0, 0, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4}
        };

        byte[][] values =
        {
            VerkleTestUtils._keyVersion, VerkleTestUtils._keyBalance, VerkleTestUtils._keyNonce,
            VerkleTestUtils._keyCodeCommitment, VerkleTestUtils._keyCodeSize,
        };

        foreach (byte[] stem in stems)
        {
            List<(byte, byte[])> batch = new();
            for (byte i = 0; i < 5; i++) batch.Add((i, values[0]));
            tree.InsertStemBatch(stem, batch);
        }
        tree.Commit();
        tree.CommitTree(0);

        VerkleProof proof = tree.CreateVerkleRangeProof(stems[0], stems[^1], out Banderwagon root);

        // bool verified = VerkleTree.VerifyVerkleRangeProof(proof, stems[0], stems[^1], stems, root, out _);
        // Assert.That(verified, Is.True);

        VerkleTree newTree = VerkleTestUtils.GetVerkleTreeForTest(DbMode.MemDb);

        Dictionary<byte[], (byte, byte[])[]> subTrees = new();
        List<(byte, byte[])> subTree = new();
        for (byte i = 0; i < 5; i++) subTree.Add((i, values[0]));
        subTrees[stems[0]] = subTree.ToArray();
        subTrees[stems[1]] = subTree.ToArray();
        subTrees[stems[2]] = subTree.ToArray();
        subTrees[stems[3]] = subTree.ToArray();

        bool isTrue =
            VerkleTree.CreateStatelessTree(newTree._verkleStateStore, proof, root, stems[0], stems[^1], subTrees);
        Assert.That(isTrue, Is.True);

        var oldTreeDumper = new VerkleTreeDumper();
        var newTreeDumper = new VerkleTreeDumper();

        tree.Accept(oldTreeDumper, new Keccak(root.ToBytes()));
        newTree.Accept(newTreeDumper, new Keccak(root.ToBytes()));

        Console.WriteLine("oldTreeDumper");
        Console.WriteLine(oldTreeDumper.ToString());
        Console.WriteLine("newTreeDumper");
        Console.WriteLine(newTreeDumper.ToString());
    }
}
