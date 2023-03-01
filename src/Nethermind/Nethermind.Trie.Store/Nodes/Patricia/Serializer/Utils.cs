// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers;
using System.Diagnostics;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Trie.Store.Nodes.Patricia.Serializer;

public interface ITrieNodeResolver
{
    /// <summary>
    /// Returns a cached and resolved <see cref="IMerkleNode"/> or a <see cref="IMerkleNode"/> with Unknown type
    /// but the hash set. The latter case allows to resolve the node later. Resolving the node means loading
    /// its RLP data from the state database.
    /// </summary>
    /// <param name="nodeKey">Keccak hash of the RLP of the node.</param>
    /// <returns></returns>
    IMerkleNode FindCachedOrUnknown(byte[] nodeKey);

    /// <summary>
    /// Loads RLP of the node.
    /// </summary>
    /// <param name="nodeKey"></param>
    /// <returns></returns>
    byte[]? LoadEncodedNodeFromTree(byte[] nodeKey);
}

public static class PatriciaUtils
{
    private const int StackallocByteThreshold = 256;

    private class TrieNodeDecoder
    {
        public static bool AllowBranchValues = false;
        private static byte[] EncodeExtension(ITrieNodeResolver tree, ExtensionNode item)
        {
            Debug.Assert(item.NodeType == NodeType.Extension,
                $"Node passed to {nameof(EncodeExtension)} is {item.NodeType}");
            Debug.Assert(item.Key is not null,
                "Extension key is null when encoding");

            byte[] hexPrefix = item.Key;
            int hexLength = HexPrefix.ByteLength(hexPrefix);
            byte[]? rentedBuffer = hexLength > StackallocByteThreshold
                ? ArrayPool<byte>.Shared.Rent(hexLength)
                : null;

            Span<byte> keyBytes = (rentedBuffer is null
                ? stackalloc byte[StackallocByteThreshold]
                : rentedBuffer)[..hexLength];

            HexPrefix.CopyToSpan(hexPrefix, isLeaf: false, keyBytes);

            int contentLength = Rlp.LengthOf(keyBytes) + (item.GetChildRlpLength());
            int totalLength = Rlp.LengthOfSequence(contentLength);
            RlpStream rlpStream = new(totalLength);
            rlpStream.StartSequence(contentLength);
            rlpStream.Encode(keyBytes);
            if (rentedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            item.WriteChildRlp(rlpStream);
            return rlpStream.Data;
        }

        private static byte[] EncodeLeaf(LeafNode node)
        {
            if (node.Key is null)
            {
                throw new ArgumentException($"Hex prefix of a leaf node is null at node {node.Keccak}");
            }

            byte[] hexPrefix = node.Key;
            int hexLength = HexPrefix.ByteLength(hexPrefix);
            byte[]? rentedBuffer = hexLength > StackallocByteThreshold
                ? ArrayPool<byte>.Shared.Rent(hexLength)
                : null;

            Span<byte> keyBytes = (rentedBuffer is null
                ? stackalloc byte[StackallocByteThreshold]
                : rentedBuffer)[..hexLength];

            HexPrefix.CopyToSpan(hexPrefix, isLeaf: true, keyBytes);
            int contentLength = Rlp.LengthOf(keyBytes) + Rlp.LengthOf(node.Value);
            int totalLength = Rlp.LengthOfSequence(contentLength);
            RlpStream rlpStream = new(totalLength);
            rlpStream.StartSequence(contentLength);
            rlpStream.Encode(keyBytes);
            if (rentedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
            rlpStream.Encode(node.Value);
            return rlpStream.Data;
        }

        private static byte[] EncodeBranch(BranchNode item)
        {
            int valueRlpLength = AllowBranchValues ? Rlp.LengthOf(item.Value) : 1;
            int contentLength = valueRlpLength + item.GetChildRlpLength();
            int sequenceLength = Rlp.LengthOfSequence(contentLength);
            RlpStream result = new RlpStream(sequenceLength);
            result.StartSequence(contentLength);
            if (AllowBranchValues) result.Encode(item.Value);
            else result.EncodeEmptyByteArray();
            item.WriteChildRlp(result);
            return result.Data!;
        }
    }
}
