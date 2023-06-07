// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.State;
using Nethermind.Verkle.Curve;
using Nethermind.Verkle.Tree;
using Nethermind.Verkle.Tree.Proofs;
using Nethermind.Verkle.Tree.Sync;
using Nethermind.Verkle.Tree.Utils;

namespace Nethermind.Synchronization.VerkleSync;

public class VerkleSyncServer
{
    private readonly IVerkleStore _store;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;

    private const long HardResponseByteLimit = 2000000;
    private const int HardResponseNodeLimit = 10000;

    public VerkleSyncServer(IVerkleStore trieStore, ILogManager logManager)
    {
        _store = trieStore ?? throw new ArgumentNullException(nameof(trieStore));
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        _logger = logManager.GetClassLogger();
    }

    public (PathWithSubTree[], VerkleProof) GetSubTreeRanges(Pedersen rootHash, Stem startingStem, Stem? limitStem, long byteLimit, out Banderwagon rootPoint)
    {
        rootPoint = default;
        IEnumerable<KeyValuePair<byte[], byte[]?>>? range = _store.GetLeafRangeIterator(startingStem.Bytes, limitStem.Bytes, rootHash, byteLimit);

        List<PathWithSubTree> nodes = new List<PathWithSubTree>();
        foreach (KeyValuePair<byte[], byte[]> keyVal in range)
        {
            LeafInSubTree leaf = new LeafInSubTree(keyVal.Key[31], keyVal.Value);
            PathWithSubTree? pathWithSubTree = new PathWithSubTree(keyVal.Key, new LeafInSubTree[] { leaf });
            nodes.Add(pathWithSubTree);
        }

        if (nodes.Count == 0) return (nodes.ToArray(), new VerkleProof());
        VerkleTree tree = new (_store);
        VerkleProof vProof =
            tree.CreateVerkleRangeProof(startingStem.Bytes, nodes[^1].Path.Bytes, out rootPoint);
        return (nodes.ToArray(), vProof);
    }
}
