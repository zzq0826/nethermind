// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;

namespace Nethermind.Verkle.Tree.Sync;

public class SubTreeRange
{
    public SubTreeRange(Hash256 stateRoot, Stem startingStem, Stem? limitStem = null, long? blockNumber = null)
    {
        RootHash = stateRoot;
        StartingStem = startingStem;
        BlockNumber = blockNumber;
        LimitStem = limitStem;
    }

    public long? BlockNumber { get; }

    /// <summary>
    ///     State Root of the verkle trie to serve
    /// </summary>
    public Hash256 RootHash { get; }

    /// <summary>
    ///     Stem of the first sub-tree to retrieve
    /// </summary>
    public Stem StartingStem { get; }

    /// <summary>
    ///     Stem after which to stop serving data
    /// </summary>
    public Stem? LimitStem { get; }

    public override string ToString()
    {
        return $"SubTreeRange: ({BlockNumber}, {RootHash}, {StartingStem}, {LimitStem})";
    }
}
