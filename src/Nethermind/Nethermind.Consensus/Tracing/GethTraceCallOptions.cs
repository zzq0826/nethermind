// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Evm.Tracing.GethStyle;

namespace Nethermind.Consensus.Tracing;

public record GethTraceCallOptions : GethTraceOptions
{
    public Dictionary<Address, AccountOverride>? AccountOverrides { get; init; }
    public BlockOverride? BlockOverride { get; init; }
}
