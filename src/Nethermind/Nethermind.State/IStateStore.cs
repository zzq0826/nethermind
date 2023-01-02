// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Trie;

namespace Nethermind.State;

public interface IStateStore: IReadOnlyStateStore
{
    new Keccak RootHash { get; set; }

    // TODO: DEPRECATE - updating and fetching full accounts
    // interface to fetch and set complete accounts
    void Set(Address address, Account account);


    void Set(StorageCell storageCell, byte[] value);


    void Set(CodeChunk codeChunk, byte[] code);
    void SetCode(Address address, ReadOnlyMemory<byte> code);

    void SetVersion(Address address, byte[] version);
    void SetBalance(Address address, byte[] balance);
    void SetNonce(Address address, byte[] nonce);
    void SetCodeHash(Address address, byte[] codeHash);
    void SetCodeSize(Address address, byte[] codeSize);

    void Commit(long blockNumber);
    void Dump(BufferedStream targetStream);
    TrieStats CollectStats();
    IStateStore MoveToStateRoot(Keccak stateRoot);
    Keccak GetStateRoot();
    void UpdateRootHash();
}


public interface IReadOnlyStateStore : IAccountStateStore
{
    Keccak RootHash { get; }

    // TODO: DEPRECATE - updating and fetching full accounts
    // interface to fetch and set complete accounts
    Account Get(Address address);

    byte[] Get(StorageCell storageCell);
    byte[] GetStorageSlot(Address address, UInt256 storageSlot);


    byte[] GetVersion(Address address);
    byte[] GetBalance(Address address);
    byte[] GetNonce(Address address);
    Keccak GetCodeHash(Address address);
    byte[] GetCodeSize(Address address);
    byte[] Get(CodeChunk codeChunk);
    byte[] GetCode(Address address);
    byte[] GetCodeChunk(Address address, UInt256 chunkId);

    byte[] GetCode(Keccak codeHash);

    Keccak GetStorageRoot(Address address);

    public bool IsContract(Address address) => GetCodeHash(address) != Keccak.OfAnEmptyString;

    /// <summary>
    /// Runs a visitor over trie.
    /// </summary>
    /// <param name="visitor">Visitor to run.</param>
    /// <param name="stateRoot">Root to run on.</param>
    /// <param name="visitingOptions">Options to run visitor.</param>
    void Accept(ITreeVisitor visitor, Keccak stateRoot, VisitingOptions? visitingOptions = null);

    bool AccountExists(Address address);

    bool IsDeadAccount(Address address);

    bool IsEmptyAccount(Address address);
}

public interface IAccountStateStore
{
    Account GetAccount(Address address);
}
