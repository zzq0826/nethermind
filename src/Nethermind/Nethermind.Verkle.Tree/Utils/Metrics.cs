// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.ComponentModel;

namespace Nethermind.Verkle.Tree.Utils;

public static class Metrics
{
    [Description("Number of pedersen hash calculations.")]
    public static long PedersenHashCalculations { get; set; }

    [Description("Number of commitment calculations.")]
    public static long CommitmentCalculations { get; set; }

    [Description("Number of commitment to field calculations")]
    public static long SetCommitmentToField { get; set; }
}
