// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Core
{
    public interface IAccountStateProvider
    {
        Account GetAccount(Address address);
    }

    public interface IVerkleAccountStateProvider
    {
        byte[] GetAccountVersion(Address address);
        byte[] GetAccountBalance(Address address);
        byte[] GetAccountNonce(Address address);
        byte[] GetAccountCodeKeccak(Address address);
        byte[] GetAccountCodeSize(Address address);

        byte[] GetAccountCodeChunk(Address address, byte chunkId);
        byte[] GetAccountStorageSlot(Address address, byte storageSlot);
    }
}
