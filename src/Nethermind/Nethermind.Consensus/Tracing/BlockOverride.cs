// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Consensus.Tracing;

public class BlockOverride
{
    public ulong? Number { get; set; }
    public UInt256? Difficulty { get; set; }
    public ulong? Time { get; set; }
    public ulong? GasLimit { get; set; }
    public Address? Coinbase { get => FeeRecipient; set => FeeRecipient = value; }
    public Address? FeeRecipient { get; set; }
    public Keccak PrevRandao { get; set; } = Keccak.Zero;
    public Keccak Random { get => PrevRandao; set => PrevRandao = value; }
    public UInt256? BaseFeePerGas { get; set; }
    public UInt256? BaseFee { get => BaseFeePerGas; set => BaseFeePerGas = value; }

    public BlockHeader GetBlockHeader(BlockHeader parent, IBlocksConfig cfg)
    {
        ulong newTime = Time ?? checked(parent.Timestamp + cfg.SecondsPerSlot);

        long newGasLimit = GasLimit switch
        {
            null => parent.GasLimit,
            <= long.MaxValue => (long)GasLimit,
            _ => throw new OverflowException($"GasLimit value is too large, max value {ulong.MaxValue}")
        };

        long newBlockNumber = Number switch
        {
            null => checked(parent.Number + 1),
            <= long.MaxValue => (long)Number,
            _ => throw new OverflowException($"Block Number value is too large, max value {ulong.MaxValue}")
        };

        UInt256 newDifficulty = Difficulty ?? (parent.Difficulty == 0 ? 0 : parent.Difficulty + 1);
        Address newFeeRecipientAddress = FeeRecipient ?? parent.Beneficiary!;
        BlockHeader? result = new(
            parent.Hash!,
            Keccak.OfAnEmptySequenceRlp,
            newFeeRecipientAddress,
            newDifficulty,
            newBlockNumber,
            newGasLimit,
            newTime,
            Array.Empty<byte>())
        {
            BaseFeePerGas = BaseFeePerGas ?? parent.BaseFeePerGas,
            MixHash = PrevRandao,
            IsPostMerge = parent.Difficulty == 0,
            TotalDifficulty = parent.TotalDifficulty + newDifficulty,
            SealEngineType = parent.SealEngineType,
        };

        return result;
    }
}
