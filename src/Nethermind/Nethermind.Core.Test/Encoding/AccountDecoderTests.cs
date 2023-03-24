// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using NUnit.Framework;

namespace Nethermind.Core.Test.Encoding
{
    [TestFixture]
    public class AccountDecoderTests
    {
        [Test]
        public void Can_read_hashes_only()
        {
            Account account = new Account(100).WithChangedCodeHash(TestItem.KeccakA).WithChangedStorageRoot(TestItem.KeccakB);
            AccountDecoder decoder = new();
            Rlp rlp = decoder.Encode(account);
            (Keccak codeHash, Keccak storageRoot) = decoder.DecodeHashesOnly(new RlpStream(rlp.Bytes));
            Assert.AreEqual(codeHash, TestItem.KeccakA);
            Assert.AreEqual(storageRoot, TestItem.KeccakB);
        }

        [Test]
        public void Roundtrip_test()
        {
            Account account = new Account(100).WithChangedCodeHash(TestItem.KeccakA).WithChangedStorageRoot(TestItem.KeccakB);
            AccountDecoder decoder = new();
            Rlp rlp = decoder.Encode(account);
            Account decoded = decoder.Decode(new RlpStream(rlp.Bytes))!;
            Assert.AreEqual((int)decoded.Balance, 100);
            Assert.AreEqual((int)decoded.Nonce, 0);
            Assert.AreEqual(decoded.CodeHash, TestItem.KeccakA);
            Assert.AreEqual(decoded.StorageRoot, TestItem.KeccakB);
        }

        /// <summary>
        /// https://ycharts.com/indicators/ethereum_cumulative_unique_addresses
        /// </summary>
        private const long AccountMainnetTotalCount = 250_000_000;

        /// <summary>
        /// https://ycharts.com/indicators/ethereum_supply
        /// </summary>
        private const long TotalEthereumSupply = 120_000_000;

        /// <summary>
        /// https://blockchair.com/ethereum/charts/total-transaction-count
        /// </summary>
        private const long TotalTransactionCount = 2_000_000_000;

        /// <summary>
        /// Assumed increase over years, like guess.
        /// </summary>
        private const long FutureMultiplier = 2;

        private const long FutureAccountCount = AccountMainnetTotalCount * FutureMultiplier;
        private static readonly UInt256 _futureTotalEthereumSupply = TotalEthereumSupply * FutureMultiplier * Unit.Ether;
        private static readonly UInt256 _futureTotalTransactionCount = TotalTransactionCount * FutureMultiplier;

        private readonly AccountDecoder _decoder = new();

        [Test, Explicit]
        public void Contract_MaxRlpSize()
        {
            // the nonce of a contract cannot be bigger than accounts count as contracts have their nonce bumped
            // only on contract construction
            UInt256 nonce = FutureAccountCount;
            UInt256 balance = _futureTotalEthereumSupply;
            Keccak keccakWithNoZeroes = Keccak.OfAnEmptySequenceRlp;

            Account biggestContract = new(nonce, balance, keccakWithNoZeroes, keccakWithNoZeroes);
            int length = _decoder.Encode(biggestContract).Length;
            Console.WriteLine($"The biggest possible contract state serialized to RLP takes: {length} bytes");
        }

        [Test, Explicit]
        public void EOA_MaxRlpSize()
        {
            UInt256 nonce = _futureTotalTransactionCount;
            UInt256 balance = _futureTotalEthereumSupply;
            Keccak storage = Keccak.OfAnEmptySequenceRlp;
            Keccak noCode = Keccak.OfAnEmptyString;

            Account biggestContract = new(nonce, balance, storage, noCode);
            int length = _decoder.Encode(biggestContract).Length;
            Console.WriteLine($"The biggest possible EOA state serialized to RLP takes: {length} bytes");
        }
    }
}
