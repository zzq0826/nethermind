// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using NUnit.Framework;

namespace Nethermind.Core.Test.Encoding;

[Explicit]
public class PaprikaTests
{
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

    [TestCase(true)]
    [TestCase(false)]
    public void Contract_MaxRlpSize(bool slim)
    {
        // the nonce of a contract cannot be bigger than accounts count as contracts have their nonce bumped
        // only on contract construction
        UInt256 nonce = FutureAccountCount;
        UInt256 balance = _futureTotalEthereumSupply;
        Keccak keccakWithNoZeroes = Keccak.OfAnEmptySequenceRlp;

        Account biggestContract = new(nonce, balance, keccakWithNoZeroes, keccakWithNoZeroes);
        int length = new AccountDecoder(slim).Encode(biggestContract).Length;
        Console.WriteLine($"The biggest possible contract state serialized to RLP takes: {length} bytes");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EOA_MaxRlpSize(bool slim)
    {
        UInt256 nonce = _futureTotalTransactionCount;
        UInt256 balance = _futureTotalEthereumSupply;
        Keccak storage = Keccak.OfAnEmptySequenceRlp;
        Keccak noCode = Keccak.OfAnEmptyString;

        Account biggestContract = new(nonce, balance, storage, noCode);
        int length = new AccountDecoder(slim).Encode(biggestContract).Length;
        Console.WriteLine($"The biggest possible EOA state serialized to RLP takes: {length} bytes");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EOA_AverageRlpSize_SlimSize(bool slim)
    {
        UInt256 nonce = TotalTransactionCount / 10000;
        UInt256 balance = 1000 * Unit.Ether;
        Keccak noStorage = Keccak.EmptyTreeHash;
        Keccak noCode = Keccak.OfAnEmptyString;

        Account eoa = new(nonce, balance, noStorage, noCode);
        int length = new AccountDecoder(slim).Encode(eoa).Length;
        Console.WriteLine($"The average possible EOA state serialized to RLP takes: {length} bytes");
    }
}
