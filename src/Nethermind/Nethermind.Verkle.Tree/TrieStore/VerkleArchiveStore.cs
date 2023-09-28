// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Collections.EliasFano;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.TrieStore;

public class VerkleArchiveStore
{
    private readonly VerkleStateStore _stateStore;
    private readonly HistoryOfAccounts _historyOfAccounts;
    private VerkleHistoryStore History { get; }

    public VerkleArchiveStore(VerkleStateStore stateStore, IDbProvider dbProvider, ILogManager logManager)
    {
        _stateStore = stateStore;
        _historyOfAccounts = new HistoryOfAccounts(dbProvider.HistoryOfAccounts);
        _stateStore.InsertBatchCompleted += OnPersistNewBlock;
        History = new VerkleHistoryStore(dbProvider, logManager);
    }

    private void OnPersistNewBlock(object? sender, InsertBatchCompleted insertBatchCompleted)
    {
        long blockNumber = insertBatchCompleted.BlockNumber;
        VerkleMemoryDb revDiff = insertBatchCompleted.ReverseDiff;
        ReadOnlyVerkleMemoryDb forwardDiff = insertBatchCompleted.ForwardDiff;
        History.InsertDiff(blockNumber, forwardDiff, revDiff);

        foreach (KeyValuePair<byte[], byte[]?> keyVal in forwardDiff.LeafTable)
            _historyOfAccounts.AppendHistoryBlockNumberForKey(new Pedersen(keyVal.Key), (ulong)blockNumber);
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment rootHash)
    {
        ulong blockNumber = (ulong)_stateStore.StateRootToBlocks[rootHash];
        EliasFano? requiredShard = _historyOfAccounts.GetAppropriateShard(key.ToArray(), blockNumber);
        if (requiredShard is null) return null;

        ulong? requiredBlock = requiredShard.Value.Predecessor(blockNumber);

        VerkleMemoryDb diff = History.GetForwardDiff((long)requiredBlock!.Value);

        diff.GetLeaf(key, out byte[]? value);
        return value;
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock < toBlock - move forward in time
    public bool GetForwardMergedDiff(long fromBlock, long toBlock, [MaybeNullWhen(false)]out VerkleMemoryDb diff)
    {
        diff = History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
        return true;
    }

    // This generates and returns a batchForwardDiff, that can be used to move the full state from fromBlock to toBlock.
    // for this fromBlock > toBlock - move back in time
    public bool GetReverseMergedDiff(long fromBlock, long toBlock, [MaybeNullWhen(false)]out VerkleMemoryDb diff)
    {
        diff = History.GetBatchDiff(fromBlock, toBlock).DiffLayer;
        return true;
    }
}
