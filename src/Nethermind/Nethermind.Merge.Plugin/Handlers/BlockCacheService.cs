//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;

namespace Nethermind.Merge.Plugin.Handlers;

public class BlockCacheService : IBlockCacheService
{
    public void Add(Block block)
    {
        if (block.ParentHash != null)
        {
            if (BlocksByParent.TryGetValue(block.ParentHash, out IList<Block>? children))
            {
                children.Add(block);
            }
            else
            {
                BlocksByParent.TryAdd(block.ParentHash, new List<Block> { block });
            }
        }
        BlockCache.TryAdd(block.Hash ?? block.CalculateHash(), block);
    }

    public Block? Get(Keccak hash)
    {
        if (BlockCache.TryGetValue(hash, out Block? block))
        {
            return block;
        }

        return null;
    }

    public void Clear()
    {
        BlockCache.Clear();
        BlocksByParent.Clear();
    }

    public IList<Block>? GetChildren(Block block)
    {
        if (BlocksByParent.TryGetValue(block.Hash ?? block.CalculateHash(), out IList<Block>? children))
        {
            return children;
        }
        return null;
    }

    public ConcurrentDictionary<Keccak, IList<Block>> BlocksByParent { get; } = new();
    public ConcurrentDictionary<Keccak, Block> BlockCache { get; } = new();
    public Keccak? ProcessDestination { get; set; }
    public Keccak? SyncingHead { get; set; }
}
