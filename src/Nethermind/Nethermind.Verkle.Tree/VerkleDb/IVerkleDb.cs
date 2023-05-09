// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Verkle.Tree.Nodes;

namespace Nethermind.Verkle.Tree.VerkleDb;

public interface IVerkleDb
{
    bool GetLeaf(byte[] key, out byte[]? value);
    bool GetInternalNode(byte[] key, out InternalNode? value);

    void SetLeaf(byte[] leafKey, byte[] leafValue);
    void SetInternalNode(byte[] internalNodeKey, InternalNode internalNodeValue);

    void RemoveLeaf(byte[] leafKey);
    void RemoveInternalNode(byte[] internalNodeKey);

    void BatchLeafInsert(IEnumerable<KeyValuePair<byte[], byte[]?>> keyLeaf);
    void BatchInternalNodeInsert(IEnumerable<KeyValuePair<byte[], InternalNode?>> internalNodeLeaf);
}
