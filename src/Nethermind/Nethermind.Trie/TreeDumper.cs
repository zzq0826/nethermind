// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using System.Text;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie
{
    public class TreeDumper : ITreeVisitor
    {
        private SimpleConsoleLogger _logger => SimpleConsoleLogger.Instance;
        private const string FilePath = "/home/paprika.txt";
        private BinaryWriter Writer = new BinaryWriter(File.Open(FilePath, FileMode.Create), Encoding.UTF8, false);

        private bool CollectLeafs(byte[] key, byte[] value)
        {
            byte[]? path = Nibbles.ToBytes(key);
            Account account = decoder.Decode(new RlpStream(value));
            _logger.Info($"COLLECTING {path.ToHexString()}");
            Writer.Write(path);
            Writer.Write(account.Balance.ToLittleEndian());
            Writer.Write(account.Nonce.ToLittleEndian());
            return true;
        }

        public void Reset()
        {
            Writer = new BinaryWriter(File.Open(FilePath, FileMode.Create), Encoding.UTF8, false);
        }

        public bool IsFullDbScan => true;

        public bool ShouldVisit(Keccak nextNode)
        {
            return true;
        }

        public void VisitTree(Keccak rootHash, TrieVisitContext trieVisitContext)
        {
        }

        private string GetPrefix(TrieVisitContext context) => string.Concat($"{GetIndent(context.Level)}", context.IsStorage ? "STORAGE " : string.Empty, $"{GetChildIndex(context)}");

        private string GetIndent(int level) => new('+', level * 2);
        private string GetChildIndex(TrieVisitContext context) => context.BranchChildIndex is null ? string.Empty : $"{context.BranchChildIndex:x2} ";

        public void VisitMissingNode(Keccak nodeHash, TrieVisitContext trieVisitContext)
        {
        }

        public void VisitBranch(TrieNode node, TrieVisitContext trieVisitContext)
        {
        }

        public void VisitExtension(TrieNode node, TrieVisitContext trieVisitContext)
        {
        }

        private AccountDecoder decoder = new();

        public void VisitLeaf(TrieNode node, TrieVisitContext trieVisitContext, byte[] value = null)
        {
            CollectLeafs(trieVisitContext.AbsolutePathNibbles.ToArray(), value);
        }

        public void VisitCode(Keccak codeHash, TrieVisitContext trieVisitContext)
        {
        }
    }
}
