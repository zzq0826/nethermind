// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Store.Nodes;

public interface ITrieNode
{
    public TreeNodeType NodeType { get; }

}

public interface IMerkleNode: ITrieNode
{

    public const long LastSeenNotSet = 0L;
    public const long NodeTypeMask = 0b11;
    public const long DirtyMask = 0b100;
    public const int DirtyShift = 2;
    public const long PersistedMask = 0b1000;
    public const int PersistedShift = 3;

    public const int LastSeenShift = 8;

    public const long IsBoundaryProofNodeMask = 0b10_0000;
    public const int IsBoundaryProofNodeShift = 5;

    public byte[]? FullRlp { get; set; }

    public Keccak? Keccak { get; internal set; }

    public byte[] Key { get; }
    public byte[] Value { get; }

    public void SeekChild(RlpStream rlpStream, int itemToSetOn);
}

public interface IVerkleNode: ITrieNode
{
    public TreeNodeType nodeType { get; }
}

public enum TreeNodeType : byte
{
    Unknown = 0,

    // merkle tree
    Branch = 1,
    Extension = 2,
    Leaf = 3,

    //verkle tree
    BranchNode = 4,
    StemNode = 5
}
