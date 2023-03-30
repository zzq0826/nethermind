// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public class HeimdallSpan : BorSpan
{
    public required ulong ChainId { get; init; }
    public required BorValidatorSet ValidatorSet { get; init; }
    public required BorValidator[] SelectedProducers { get; init; }
}