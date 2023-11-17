// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Evm.Tracing.GethStyle;

public class GethLikeJavascriptTrace
{
    private static readonly object _empty = new { };
    public object Value { get; init; } = _empty;

    public override string ToString() => Value.ToString() ?? string.Empty;
}
