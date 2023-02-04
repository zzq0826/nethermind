// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using Nethermind.Verkle.Nodes;

namespace Nethermind.Verkle.VerkleDb;

public interface IVerkleMemDb
{
    public ConcurrentDictionary<byte[], byte[]?> LeafTable { get; }
    public ConcurrentDictionary<byte[], SuffixTree?> StemTable { get; }
    public ConcurrentDictionary<byte[], InternalNode?> BranchTable { get; }
}
