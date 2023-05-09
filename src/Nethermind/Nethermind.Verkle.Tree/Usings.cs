// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

global using InternalStore = System.Collections.Concurrent.ConcurrentDictionary<byte[], Nethermind.Verkle.Tree.Nodes.InternalNode?>;
global using LeafStore = System.Collections.Concurrent.ConcurrentDictionary<byte[], byte[]?>;
global using VerkleUtils = Nethermind.Verkle.Tree.Utils.VerkleUtils;
global using VerkleNodeType = Nethermind.Verkle.Tree.Nodes.VerkleNodeType;

