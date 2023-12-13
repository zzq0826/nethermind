// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Trie.Pruning;

public class NodeStorageFactory: INodeStorageFactory
{
    public INodeStorage WrapKeyValueStore(IKeyValueStore keyValueStore)
    {
        return new NodeStorage(keyValueStore);
    }
}

public interface INodeStorageFactory
{
    INodeStorage WrapKeyValueStore(IKeyValueStore keyValueStore);
}
