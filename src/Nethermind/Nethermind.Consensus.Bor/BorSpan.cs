// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Consensus.Bor;

public class BorSpan
{
    public long Number { get; init; }
    public long StartBlock { get; init; }
    public long EndBlock { get; init; }
}