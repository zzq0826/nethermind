// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using Nethermind.Core.Collections.EliasFano;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.TrieStore;
using Nethermind.Verkle.Tree.Utils;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.Verkle.Tree.History.V2;

public class VerkleArchiveStore
{
    public int BlockChunks
    {
        get => _historyOfAccounts.BlocksChunks;
        set => _historyOfAccounts.BlocksChunks = value;
    }

    private readonly VerkleStateStore _stateStore;
    private readonly HistoryOfAccounts _historyOfAccounts;
    private ILeafChangeSet ChangeSet { get; }

    public VerkleArchiveStore(VerkleStateStore stateStore, IDbProvider dbProvider, ILogManager logManager)
    {
        _stateStore = stateStore;
        _historyOfAccounts = new HistoryOfAccounts(dbProvider.HistoryOfAccounts);
        _stateStore.InsertBatchCompleted += OnPersistNewBlock;
        ChangeSet = new LeafChangeSet(dbProvider, logManager);
    }

    private void OnPersistNewBlock(object? sender, InsertBatchCompleted insertBatchCompleted)
    {
        Console.WriteLine(
            $"Inserting after commit: BN:{insertBatchCompleted.BlockNumber} FD:{insertBatchCompleted.ForwardDiff.LeafTable.Count}");
        long blockNumber = insertBatchCompleted.BlockNumber;
        ReadOnlyVerkleMemoryDb forwardDiff = insertBatchCompleted.ForwardDiff;
        ChangeSet.InsertDiff(blockNumber, forwardDiff.LeafTable);

        foreach (KeyValuePair<byte[], byte[]?> keyVal in forwardDiff.LeafTable)
            _historyOfAccounts.AppendHistoryBlockNumberForKey(new Pedersen(keyVal.Key), (ulong)blockNumber);
    }

    public byte[]? GetLeaf(ReadOnlySpan<byte> key, VerkleCommitment rootHash)
    {
        ulong blockNumber = (ulong)_stateStore.StateRootToBlocks[rootHash];
        EliasFano? requiredShard = _historyOfAccounts.GetAppropriateShard(key.ToArray(), blockNumber);
        if (requiredShard is null) return null;

        ulong? requiredBlock = requiredShard.Value.Predecessor(blockNumber);

        return ChangeSet.GetLeaf((long)requiredBlock!.Value, key);
    }
}
