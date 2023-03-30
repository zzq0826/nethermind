// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public class BorSpan
{
    public required long Number { get; init; }
    public required long StartBlock { get; init; }
    public required long EndBlock { get; init; }
}