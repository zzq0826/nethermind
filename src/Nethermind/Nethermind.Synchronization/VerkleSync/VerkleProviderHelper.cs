// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Crypto;
using Nethermind.State;
using Nethermind.State.Snap;
using Nethermind.Verkle.Tree.Sync;

namespace Nethermind.Synchronization.VerkleSync;

public class VerkleProviderHelper
{
    public static (AddRangeResult result, bool moreChildrenToRight) AddSubTreeRange(
            VerkleStateTree tree,
            long blockNumber,
            byte[] expectedRootHash,
            byte[] startingHash,
            byte[] limitHash,
            PathWithSubTree[] subTrees,
            byte[][] proofs = null
        )
    {
        byte[] lastStem = subTrees[^1].Path;

        foreach (PathWithSubTree? subTree in subTrees)
        {
            List<(byte, byte[])> batch = new();
            for (byte i = 0; i < subTree.SubTree.Length; i++)
            {
                batch.Add((i, subTree.SubTree[i]));
            }
            tree.InsertStemBatch(subTree.Path, batch.AsEnumerable());
            tree.Commit();
        }

        if (tree.StateRoot != expectedRootHash)
        {
            return (AddRangeResult.DifferentRootHash, true);
        }

        tree.CommitTree(blockNumber);

        return (AddRangeResult.OK, true);
    }


}
