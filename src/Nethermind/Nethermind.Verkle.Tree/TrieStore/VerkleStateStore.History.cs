// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Verkle.Tree.History.V1;
using Nethermind.Verkle.Tree.TrieNodes;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

internal partial class VerkleStateStore<TCache>
{
    [Obsolete("should not be used - can be used in extreme cases to correct state")]
    // use the batch diff to move the full state back in time to access historical state.
    public void ApplyDiffLayer(BatchChangeSet changeSet)
    {
        if (changeSet.FromBlockNumber != LastPersistedBlockNumber)
            throw new ArgumentException(
                $"This case should not be possible. Diff fromBlock should be equal to persisted block number. FullStateBlock:{LastPersistedBlockNumber}!=fromBlock:{changeSet.FromBlockNumber}",
                nameof(changeSet.FromBlockNumber));

        VerkleMemoryDb reverseDiff = changeSet.DiffLayer;

        foreach (KeyValuePair<byte[], byte[]?> entry in reverseDiff.LeafTable)
        {
            reverseDiff.GetLeaf(entry.Key, out var node);
            if (node is null)
                Storage.RemoveLeaf(entry.Key);
            else
                Storage.SetLeaf(entry.Key, node);
        }

        foreach (KeyValuePair<byte[], InternalNode?> entry in reverseDiff.InternalTable)
        {
            reverseDiff.GetInternalNode(entry.Key, out InternalNode? node);
            if (node is null)
                Storage.RemoveInternalNode(entry.Key);
            else
                Storage.SetInternalNode(entry.Key, node);
        }

        LastPersistedBlockNumber = changeSet.ToBlockNumber;
    }
}
