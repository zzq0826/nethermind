// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Verkle.Tree.TrieStore;

namespace Nethermind.State;

public class TransitionWorldState: VerkleWorldState
{
    private Keccak FinalizedMerkleStateRoot { get; }
    private readonly StateReader _merkleStateReader;

    public TransitionWorldState(StateReader merkleStateReader, Keccak finalizedStateRoot, VerkleStateTree verkleTree,
        IKeyValueStore? codeDb, ILogManager? logManager) : base(
        new TransitionStorageProvider(merkleStateReader, finalizedStateRoot, verkleTree, logManager), verkleTree,
        codeDb, logManager)
    {
        _merkleStateReader = merkleStateReader;
        FinalizedMerkleStateRoot = finalizedStateRoot;
    }

    protected override Account? GetAndAddToCache(Address address)
    {
        Account? account = GetState(address) ?? _merkleStateReader.GetAccount(FinalizedMerkleStateRoot, address);
        if (account is not null)
        {
            PushJustCache(address, account);
        }
        else
        {
            // just for tracing - potential perf hit, maybe a better solution?
            _readsForTracing.Add(address);
        }

        return account;
    }

    public override byte[] GetCodeChunk(Address codeOwner, UInt256 chunkId)
    {
        return base.GetCodeChunk(codeOwner, chunkId);
    }
}


internal class TransitionStorageProvider : VerklePersistentStorageProvider
{
    private Keccak FinalizedMerkleStateRoot { get; }
    private readonly StateReader _merkleStateReader;

    public TransitionStorageProvider(StateReader merkleStateReader, Keccak finalizedStateRoot, VerkleStateTree tree, ILogManager? logManager): base(tree, logManager)
    {
        _merkleStateReader = merkleStateReader;
        FinalizedMerkleStateRoot = finalizedStateRoot;
    }

    protected override byte[] LoadFromTree(StorageCell storageCell)
    {
        Db.Metrics.StorageTreeReads++;
        Pedersen key = AccountHeader.GetTreeKeyForStorageSlot(storageCell.Address.Bytes, storageCell.Index);
        byte[]? value = _verkleTree.Get(key) ?? _merkleStateReader.GetStorage(FinalizedMerkleStateRoot, in storageCell);
        value ??= Array.Empty<byte>();
        PushToRegistryOnly(storageCell, value);
        return value;
    }

}
