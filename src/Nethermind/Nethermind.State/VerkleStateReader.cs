// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Trie;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.TrieStore;
using Metrics = Nethermind.Db.Metrics;

namespace Nethermind.State;

public class VerkleStateReader : IStateReader
{
    private readonly IKeyValueStore _codeDb;
    private readonly ILogger _logger;
    private readonly VerkleStateTree _state;

    public VerkleStateReader(VerkleStateTree verkleTree, IKeyValueStore? codeDb, ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<StateReader>() ?? throw new ArgumentNullException(nameof(logManager));
        _codeDb = codeDb ?? throw new ArgumentNullException(nameof(codeDb));
        _state = verkleTree;
    }

    public VerkleStateReader(IVerkleTrieStore verkleTree, IKeyValueStore? codeDb, ILogManager? logManager)
    {
        _logger = logManager?.GetClassLogger<StateReader>() ?? throw new ArgumentNullException(nameof(logManager));
        _codeDb = codeDb ?? throw new ArgumentNullException(nameof(codeDb));
        _state = new VerkleStateTree(verkleTree, logManager);;
    }

    public Account? GetAccount(Hash256 stateRoot, Address address)
    {
        return GetState(stateRoot, address);
    }

    public byte[] GetStorage(Address address, in UInt256 index)
    {
        return _state.Get(address, index);
    }

    public byte[] GetStorage(Hash256 storageRoot, in UInt256 index)
    {
        throw new NotImplementedException();
    }

    public byte[]? GetStorage(Hash256 stateRoot, in StorageCell cell) => GetStorage(cell.Address, cell.Index);

    public UInt256 GetBalance(Hash256 stateRoot, Address address)
    {
        return GetState(stateRoot, address)?.Balance ?? UInt256.Zero;
    }

    public byte[]? GetCode(Hash256 codeHash)
    {
        if (codeHash == Keccak.OfAnEmptyString)
        {
            return Array.Empty<byte>();
        }

        return _codeDb[codeHash.Bytes];
    }

    public void RunTreeVisitor(ITreeVisitor treeVisitor, Hash256 rootHash, VisitingOptions? visitingOptions = null)
    {
        _state.Accept(treeVisitor, rootHash, visitingOptions);
    }

    public bool HasStateForRoot(Hash256 stateRoot)
    {
        return _state.HasStateForStateRoot(stateRoot);
    }

    public byte[] GetCode(Hash256 stateRoot, Address address)
    {
        Account? account = GetState(stateRoot, address);
        return account is null ? Array.Empty<byte>() : GetCode(account.CodeHash);
    }

    private Account? GetState(Hash256 stateRoot, Address address)
    {
        if (stateRoot == Keccak.EmptyTreeHash || stateRoot == Pedersen.Zero)
        {
            return null;
        }

        Metrics.StateTreeReads++;
        Account? account = _state.Get(address, stateRoot);
        return account;
    }
}
