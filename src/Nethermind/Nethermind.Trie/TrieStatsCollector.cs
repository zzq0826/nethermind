// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Trie
{
    public class TrieStatsCollector : ITreeVisitor<ConventionalContext>
    {
        private readonly IKeyValueStore _codeKeyValueStore;
        private int _lastAccountNodeCount = 0;

        private readonly ILogger _logger;

        public TrieStatsCollector(IKeyValueStore codeKeyValueStore, ILogManager logManager)
        {
            _codeKeyValueStore = codeKeyValueStore ?? throw new ArgumentNullException(nameof(codeKeyValueStore));
            _logger = logManager.GetClassLogger();
        }

        public TrieStats Stats { get; } = new();

        public bool IsFullDbScan => true;

        public bool ShouldVisit(in ConventionalContext ctx, Hash256 nextNode)
        {
            return true;
        }

        public void VisitTree(in ConventionalContext ctx, Hash256 rootHash, TrieVisitContext trieVisitContext) { }

        public void VisitMissingNode(in ConventionalContext ctx, Hash256 nodeHash, TrieVisitContext trieVisitContext)
        {
            if (trieVisitContext.IsStorage)
            {
                Interlocked.Increment(ref Stats._missingStorage);
            }
            else
            {
                Interlocked.Increment(ref Stats._missingState);
            }

            IncrementLevel(ctx, trieVisitContext);
        }

        public void VisitBranch(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext)
        {
            if (trieVisitContext.IsStorage)
            {
                Interlocked.Add(ref Stats._storageSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._storageBranchCount);
            }
            else
            {
                Interlocked.Add(ref Stats._stateSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._stateBranchCount);
            }

            IncrementLevel(ctx, trieVisitContext);
        }

        public void VisitExtension(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext)
        {
            if (trieVisitContext.IsStorage)
            {
                Interlocked.Add(ref Stats._storageSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._storageExtensionCount);
            }
            else
            {
                Interlocked.Add(ref Stats._stateSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._stateExtensionCount);
            }

            IncrementLevel(ctx, trieVisitContext);
        }

        public void VisitLeaf(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext, ReadOnlySpan<byte> value)
        {
            if (Stats.NodesCount - _lastAccountNodeCount > 1_000_000)
            {
                _lastAccountNodeCount = Stats.NodesCount;
                _logger.Warn($"Collected info from {Stats.NodesCount} nodes. Missing CODE {Stats.MissingCode} STATE {Stats.MissingState} STORAGE {Stats.MissingStorage}");
            }

            if (trieVisitContext.IsStorage)
            {
                Interlocked.Add(ref Stats._storageSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._storageLeafCount);
            }
            else
            {
                Interlocked.Add(ref Stats._stateSize, node.FullRlp.Length);
                Interlocked.Increment(ref Stats._accountCount);
            }

            IncrementLevel(ctx, trieVisitContext);
        }

        public void VisitCode(in ConventionalContext ctx, Hash256 codeHash, TrieVisitContext trieVisitContext)
        {
            byte[] code = _codeKeyValueStore[codeHash.Bytes];
            if (code is not null)
            {
                Interlocked.Add(ref Stats._codeSize, code.Length);
                Interlocked.Increment(ref Stats._codeCount);
            }
            else
            {
                Interlocked.Increment(ref Stats._missingCode);
            }

            IncrementLevel(ctx.Level, trieVisitContext, Stats._codeLevels);
        }

        private void IncrementLevel(in ConventionalContext ctx, TrieVisitContext trieVisitContext)
        {
            int[] levels = trieVisitContext.IsStorage ? Stats._storageLevels : Stats._stateLevels;
            IncrementLevel(ctx.Level, trieVisitContext, levels);
        }

        private static void IncrementLevel(int level, TrieVisitContext trieVisitContext, int[] levels)
        {
            Interlocked.Increment(ref levels[level]);
        }
    }
}
