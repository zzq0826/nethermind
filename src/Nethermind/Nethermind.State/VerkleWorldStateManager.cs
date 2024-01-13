// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;
using Nethermind.Verkle.Tree.TrieStore;
using Nethermind.Verkle.Tree.VerkleDb;

namespace Nethermind.State;

public class VerkleWorldStateManager: ReadOnlyVerkleWorldStateManager
{
    private readonly IWorldState _worldState;
    private readonly IVerkleTrieStore _trieStore;

    public VerkleWorldStateManager(
        IWorldState worldState,
        IVerkleTrieStore trieStore,
        IDbProvider dbProvider,
        ILogManager logManager
    ) : base(dbProvider, trieStore.AsReadOnly(new VerkleMemoryDb()), logManager)
    {
        _worldState = worldState;
        _trieStore = trieStore;
    }

    public override IWorldState GlobalWorldState => _worldState;

    public override event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add => _trieStore.ReorgBoundaryReached += value;
        remove => _trieStore.ReorgBoundaryReached -= value;
    }
}
