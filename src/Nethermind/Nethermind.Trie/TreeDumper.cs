// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Text;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie
{
    public class TreeDumper : ITreeVisitor<ConventionalContext>
    {
        private readonly StringBuilder _builder = new();

        public void Reset()
        {
            _builder.Clear();
        }

        public bool IsFullDbScan => true;

        public bool ShouldVisit(in ConventionalContext ctx, Hash256 nextNode)
        {
            return true;
        }

        public void VisitTree(in ConventionalContext ctx, Hash256 rootHash, TrieVisitContext trieVisitContext)
        {
            if (rootHash == Keccak.EmptyTreeHash)
            {
                _builder.AppendLine("EMPTY TREE");
            }
            else
            {
                _builder.AppendLine(trieVisitContext.IsStorage ? "STORAGE TREE" : "STATE TREE");
            }
        }

        private static string GetPrefix(in ConventionalContext ctx, TrieVisitContext context) => string.Concat($"{GetIndent(ctx.Level)}", context.IsStorage ? "STORAGE " : string.Empty, $"{GetChildIndex(context)}");

        private static string GetIndent(int level) => new('+', level * 2);
        private static string GetChildIndex(TrieVisitContext context) => context.BranchChildIndex is null ? string.Empty : $"{context.BranchChildIndex:x2} ";

        public void VisitMissingNode(in ConventionalContext ctx, Hash256 nodeHash, TrieVisitContext trieVisitContext)
        {
            _builder.AppendLine($"{GetIndent(ctx.Level)}{GetChildIndex(trieVisitContext)}MISSING {nodeHash}");
        }

        public void VisitBranch(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext)
        {
            _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}BRANCH | -> {KeccakOrRlpStringOfNode(node)}");
        }

        public void VisitExtension(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext)
        {
            _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}EXTENSION {Nibbles.FromBytes(node.Key).ToPackedByteArray().ToHexString(false)} -> {KeccakOrRlpStringOfNode(node)}");
        }

        private readonly AccountDecoder decoder = new();

        public void VisitLeaf(in ConventionalContext ctx, TrieNode node, TrieVisitContext trieVisitContext, ReadOnlySpan<byte> value)
        {
            string leafDescription = trieVisitContext.IsStorage ? "LEAF " : "ACCOUNT ";
            _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}{leafDescription} {Nibbles.FromBytes(node.Key).ToPackedByteArray().ToHexString(false)} -> {KeccakOrRlpStringOfNode(node)}");
            Rlp.ValueDecoderContext valueDecoderContext = new(value);
            if (!trieVisitContext.IsStorage)
            {
                Account account = decoder.Decode(ref valueDecoderContext);
                _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}  NONCE: {account.Nonce}");
                _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}  BALANCE: {account.Balance}");
                _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}  IS_CONTRACT: {account.IsContract}");
            }
            else
            {
                _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}  VALUE: {valueDecoderContext.DecodeByteArray().ToHexString(true, true)}");
            }
        }

        public void VisitCode(in ConventionalContext ctx, Hash256 codeHash, TrieVisitContext trieVisitContext)
        {
            _builder.AppendLine($"{GetPrefix(ctx, trieVisitContext)}CODE {codeHash}");
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        private static string? KeccakOrRlpStringOfNode(TrieNode node)
        {
            return node.Keccak is not null ? node.Keccak!.Bytes.ToHexString() : node.FullRlp.AsSpan().ToHexString();
        }
    }
}
