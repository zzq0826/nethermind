// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Consensus.Processing;

public class WarmUpStep: IBlockPreprocessorStep
{
    private readonly IWorldState _worldState;

    public WarmUpStep(IWorldState worldState)
    {
        _worldState = worldState;
    }

    public void RecoverData(Block block)
    {
        // Don't block, let it run together with EVM.
        Task.Run(async () =>
        {
            await WarmupData(block);
        });
    }

    private async Task WarmupData(Block block)
    {
        await Task.WhenAll(block.Transactions.Select((tx) => Task.Run(() => WarmupData(tx))));
    }

    private Task WarmupData(Transaction tx)
    {
        if (tx.SenderAddress != null) _worldState.GetAccount(tx.SenderAddress);
        if (tx.To != null) _worldState.GetAccount(tx.To);
        if (tx.AccessList != null)
        {
            foreach (KeyValuePair<Address, IReadOnlySet<UInt256>> access in tx.AccessList.Data)
            {
                Address address = access.Key;
                foreach (UInt256 key in access.Value)
                {
                    _worldState.Get(new StorageCell(address, key));
                }
            }
        }

        return Task.CompletedTask;
    }
}
