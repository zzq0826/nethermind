// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public class BorValidatorSet
{
    public required BorValidator[] Validators { get; init; }
    public required BorValidator Proposer { get; init; }
}