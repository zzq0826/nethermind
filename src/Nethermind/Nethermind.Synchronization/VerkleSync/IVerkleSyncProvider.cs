// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.Sync;

namespace Nethermind.Synchronization.VerkleSync;

public interface IVerkleSyncProvider
{
    (VerkleSyncBatch request, bool finished) GetNextRequest();
    bool CanSync();

    AddRangeResult AddSubTreeRange(SubTreeRange request, SubTreesAndProofs response);
    AddRangeResult AddSubTreeRange(long blockNumber, byte[] expectedRootHash, byte[] startingStem, PathWithSubTree[] subTrees, byte[][] proofs = null, byte[] limitStem = null!);

    void RefreshLeafs(LeafToRefreshRequest request, byte[][] response);

    void RetryRequest(VerkleSyncBatch batch);

    bool IsVerkleGetRangesFinished();
    void UpdatePivot();
}
