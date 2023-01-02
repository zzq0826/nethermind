// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Trie;

namespace Nethermind.State;

public interface IStateStore
{
    Keccak RootHash { get; set; }
    // TODO: DEPRECATE - updating and fetching full accounts
    // interface to fetch and set complete accounts
    Account Get(Address address);
    void Set(Address address, Account account);

    byte[] Get(StorageCell storageCell);
    byte[] GetStorageSlot(Address address, UInt256 storageSlot);
    void Set(StorageCell storageCell, byte[] value);

    byte[] Get(CodeChunk codeChunk);
    byte[] GetCode(Address address);
    byte[] GetCodeChunk(Address address, UInt256 chunkId);
    void Set(CodeChunk codeChunk, byte[] code);
    void SetCode(Address address, ReadOnlyMemory<byte> code);

    Keccak UpdateCode(ReadOnlyMemory<byte> code);
    byte[] GetCode(Keccak codeHash);

    byte[] GetVersion(Address address);
    void SetVersion(Address address, byte[] version);

    byte[] GetBalance(Address address);
    void SetBalance(Address address, byte[] balance);

    byte[] GetNonce(Address address);
    void SetNonce(Address address, byte[] nonce);

    byte[] GetCodeHash(Address address);
    void SetCodeHash(Address address, byte[] codeHash);

    byte[] GetCodeSize(Address address);
    void SetCodeSize(Address address, byte[] codeSize);


    void Commit(long blockNumber);
    void Dump(BufferedStream targetStream);
    TrieStats CollectStats();
    IStateStore MoveToStateRoot(Keccak stateRoot);
    Keccak GetStateRoot();
    void UpdateRootHash();

    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null);
}
