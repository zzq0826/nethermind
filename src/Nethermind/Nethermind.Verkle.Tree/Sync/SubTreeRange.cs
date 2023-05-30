// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Verkle.Tree.Sync;

public class SubTreeRange
{
    public SubTreeRange(Pedersen stateRoot, byte[] startingStem, byte[]? limitStem = null, long? blockNumber = null)
    {
        RootHash = stateRoot;
        StartingStem = startingStem;
        BlockNumber = blockNumber;
        LimitStem = limitStem;
    }

    public long? BlockNumber { get; }

    /// <summary>
    /// State Root of the verkle trie to serve
    /// </summary>
    public Pedersen RootHash { get; }

    /// <summary>
    /// Stem of the first sub-tree to retrieve
    /// </summary>
    public byte[] StartingStem { get; }

    /// <summary>
    /// Stem after which to stop serving data
    /// </summary>
    public byte[]? LimitStem { get; }

    public override string ToString()
    {
        return $"SubTreeRange: ({BlockNumber}, {RootHash}, {StartingStem}, {LimitStem})";
    }
}
