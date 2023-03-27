// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public class HeimdallSpan : BorSpan
{
    public ulong ChainId { get; init; }
    public BorValidatorSet? ValidatorSet { get; init; }
    public BorValidator[]? SelectedProducers { get; init; }
}