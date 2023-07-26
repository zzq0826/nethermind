// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Core.Verkle;
using Nethermind.Trie;
using Nethermind.Verkle.Tree.Sync;

namespace Nethermind.Verkle.Tree;

public interface IVerkleTree
{
    public Pedersen StateRoot { get; set; }
    public bool MoveToStateRoot(Pedersen stateRoot);
    public byte[]? Get(Pedersen key);
    public void Insert(Pedersen key, ReadOnlySpan<byte> value);
    public void InsertStemBatch(ReadOnlySpan<byte> stem, IEnumerable<(byte, byte[])> leafIndexValueMap);
    public void InsertStemBatch(ReadOnlySpan<byte> stem, IEnumerable<LeafInSubTree> leafIndexValueMap);
    public void InsertStemBatch(in Stem stem, IEnumerable<LeafInSubTree> leafIndexValueMap);


    public void Commit(bool forSync = false);
    public void CommitTree(long blockNumber);
    public void Accept(ITreeVisitor visitor, Keccak rootHash, VisitingOptions? visitingOptions = null);
    public void Accept(IVerkleTreeVisitor visitor, Pedersen rootHash, VisitingOptions? visitingOptions = null);
}
