// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Trie;

namespace Nethermind.State
{
    public interface IReadOnlyStateProvider : IAccountStateProvider
    {
        Keccak StateRoot { get; }

        UInt256 GetVersion(Address address);
        UInt256 GetBalance(Address address);
        UInt256 GetNonce(Address address);
        Keccak GetCodeHash(Address address);
        UInt256 GetCodeSize(Address address);

        byte[] GetCode(Address address);
        byte[] GetCodeChunk(Address address, UInt256 chunkId);

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
}
