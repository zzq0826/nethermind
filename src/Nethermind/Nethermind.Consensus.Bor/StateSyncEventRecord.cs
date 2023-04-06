// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Consensus.Bor;

public class StateSyncEventRecord
{
    public required ulong Id { get; init; }
    public required DateTimeOffset Time { get; init; }
    public required Address ContractAddress { get; init; }
    public required byte[] Data { get; init; }
    public required Keccak TxHash { get; init; }
    public required long LogIndex { get; init; }
    public required long ChainId { get; init; }
}