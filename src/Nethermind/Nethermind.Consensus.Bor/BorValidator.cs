// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public class BorValidator
{
    public required long Id { get; init; }
    public required Address Address { get; init; }
    public required long VotingPower { get; init; }
    public required long ProposerPriority { get; init; }
}