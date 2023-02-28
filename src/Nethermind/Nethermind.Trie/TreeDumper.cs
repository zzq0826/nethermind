// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.IO;
using System.Text;
using System.Threading;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Nethermind.Trie
{
    public class TreeDumper : ITreeVisitor
    {
        private SimpleConsoleLogger _logger => SimpleConsoleLogger.Instance;

        private bool CollectLeafs(byte[] rootHash, byte[] key, byte[] value, bool isStorage)
        {
            string leafDescription = isStorage ? "LEAF " : "ACCOUNT ";
            _logger.Info($"COLLECTING {leafDescription}");
            if (isStorage) key = key[64..];
            File.AppendAllLines($"/media/sherlock/__root1/gnosisStateDump/{rootHash.ToHexString()}.txt", new []{$"{Nibbles.ToBytes(key).ToHexString()}:{value.ToHexString()}"});
            return true;
        }

        public void Reset()
        {
        }

        public bool ShouldVisit(Keccak nextNode)
        {
            return true;
        }

        public void VisitTree(Keccak rootHash, TrieVisitContext trieVisitContext)
        {
            if (rootHash == Keccak.EmptyTreeHash)
            {
                _logger.Info("EMPTY TREEE");
            }
            else
            {
                _logger.Info(trieVisitContext.IsStorage ? "STORAGE TREE" : "STATE TREE");
            }
        }

        private string GetPrefix(TrieVisitContext context) => string.Concat($"{GetIndent(context.Level)}", context.IsStorage ? "STORAGE " : string.Empty, $"{GetChildIndex(context)}");

        private string GetIndent(int level) => new('+', level * 2);
        private string GetChildIndex(TrieVisitContext context) => context.BranchChildIndex is null ? string.Empty : $"{context.BranchChildIndex:x2} ";

        public void VisitMissingNode(Keccak nodeHash, TrieVisitContext trieVisitContext)
        {
            _logger.Info($"{GetIndent(trieVisitContext.Level)}{GetChildIndex(trieVisitContext)}MISSING {nodeHash}");
            throw new ArgumentException("node not found");
        }

        public void VisitBranch(TrieNode node, TrieVisitContext trieVisitContext)
        {
            // _logger.Info($"{GetPrefix(trieVisitContext)}BRANCH | -> {(node.Keccak?.Bytes ?? node.FullRlp)?.ToHexString()}");
        }

        public void VisitExtension(TrieNode node, TrieVisitContext trieVisitContext)
        {
            // _logger.Info($"{GetPrefix(trieVisitContext)}EXTENSION {Nibbles.FromBytes(node.Key).ToPackedByteArray().ToHexString(false)} -> {(node.Keccak?.Bytes ?? node.FullRlp)?.ToHexString()}");
        }

        public void VisitLeaf(TrieNode node, TrieVisitContext trieVisitContext, byte[] value = null)
        {
            CollectLeafs(trieVisitContext.RootHash.Bytes, trieVisitContext.AbsolutePathNibbles.ToArray(), value, trieVisitContext.IsStorage);
            // string leafDescription = trieVisitContext.IsStorage ? "LEAF " : "ACCOUNT ";
            // _logger.Info($"{leafDescription}");
        }

        public void VisitCode(Keccak codeHash, TrieVisitContext trieVisitContext)
        {
            // _logger.Info($"{GetPrefix(trieVisitContext)}CODE {codeHash}");
        }

        public override string ToString()
        {
            return "";
        }
    }
}
