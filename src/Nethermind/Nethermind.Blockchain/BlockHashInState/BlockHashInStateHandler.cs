// SPDX-FileCopyrightText:2023 Demerzel Solutions Limited
// SPDX-License-Identifier:LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Blockchain.BlockHashInState;

public interface IBlockHashInStateHandler
{
    public void AddBlockHashToState(Block block, IReleaseSpec spec, IWorldState stateProvider);
}

public class BlockHashInStateHandler: IBlockHashInStateHandler
{
    private static readonly Address DefaultHistoryStorageAddress = new Address("0xfffffffffffffffffffffffffffffffffffffffe");

    public void AddBlockHashToState(Block block, IReleaseSpec spec, IWorldState stateProvider)
    {
        if (block.IsGenesis ||
            block.Header.ParentHash is null) return;
        Address? eip2935Account = DefaultHistoryStorageAddress;

        Hash256 parentBlockHash = block.Header.ParentHash;
        var blockIndex = new UInt256((ulong)block.Number - 1);

        StorageCell blockHashStoreCell = new(eip2935Account, blockIndex);
        stateProvider.Set(blockHashStoreCell, parentBlockHash.Bytes.WithoutLeadingZeros().ToArray());
    }
}
