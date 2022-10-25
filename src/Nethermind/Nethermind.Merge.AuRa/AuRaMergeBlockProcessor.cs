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

using System;
using System.Collections.Generic;
using System.Linq;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus;
using Nethermind.Consensus.AuRa;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Validators;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Core.Extensions;
using Nethermind.Core.Collections;

namespace Nethermind.Merge.AuRa;

public class AuRaMergeBlockProcessor : AuRaBlockProcessor
{
    private readonly Address _ownerAddress = new("0xb03a86b3126157c039b55e21d378587ccfc04d45");
    private readonly UInt256 _rewardContractOwnerSlot =
        new(Bytes.FromHexString("0xb53127684a568b3173ae13b9f8a6016e243e63b6e8ee1178d6a717850b5d6103"), isBigEndian: true);

    private readonly IPoSSwitcher _poSSwitcher;
    private readonly List<(long, Address)> _auraRewardContracts;

    public AuRaMergeBlockProcessor(
        IPoSSwitcher poSSwitcher,
        ISpecProvider specProvider,
        IBlockValidator blockValidator,
        IRewardCalculator rewardCalculator,
        IBlockProcessor.IBlockTransactionsExecutor blockTransactionsExecutor,
        IStateProvider stateProvider,
        IStorageProvider storageProvider,
        IReceiptStorage receiptStorage,
        ILogManager logManager,
        IBlockTree blockTree,
        AuRaParameters auraParams,
        ITxFilter? txFilter = null,
        AuRaContractGasLimitOverride? gasLimitOverride = null,
        ContractRewriter? contractRewriter = null
    ) : base(
            specProvider,
            blockValidator,
            rewardCalculator,
            blockTransactionsExecutor,
            stateProvider,
            storageProvider,
            receiptStorage,
            logManager,
            blockTree,
            txFilter,
            gasLimitOverride,
            contractRewriter
        )
    {
        _poSSwitcher = poSSwitcher;

        _auraRewardContracts = new();

        if (auraParams.BlockRewardContractTransitions is not null)
        {
            var transitions = auraParams.BlockRewardContractTransitions;
            _auraRewardContracts.AddRange(transitions.Select(t => (t.Key, t.Value)));
            _auraRewardContracts.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        }

        if (auraParams.BlockRewardContractAddress is not null)
        {
            var transition = auraParams.BlockRewardContractTransition ?? 0;
            if (_auraRewardContracts.Count > 0 && transition > (_auraRewardContracts.First().Item1))
            {
                throw new ArgumentException($"{nameof(auraParams.BlockRewardContractTransition)} provided for {nameof(auraParams.BlockRewardContractAddress)} is higher than first {nameof(auraParams.BlockRewardContractTransitions)}.");
            }

            _auraRewardContracts.Insert(0, (transition, auraParams.BlockRewardContractAddress));
        }
    }

    protected override TxReceipt[] ProcessBlock(Block block, IBlockTracer blockTracer, ProcessingOptions options)
    {
        if (_poSSwitcher.IsPostMerge(block.Header))
        {
            if (_auraRewardContracts.TryGetSearchedItem(block.Number, (b, c) => b.CompareTo(c.Item1), out var contract))
            {
                StorageCell rewardContractOwnerStorageCell = new(contract.Item2, _rewardContractOwnerSlot);

                _logger.Info($"Changing owner BlockRewardContract({rewardContractOwnerStorageCell.Address}) to {_ownerAddress}");

                _storageProvider.Set(rewardContractOwnerStorageCell, Bytes.PadLeft(_ownerAddress.Bytes, 32));

                return PostMergeProcessBlock(block, blockTracer, options);
            }
            else
            {
                throw new Exception("No reward contract in this AuRa chain. Change the code to allow this if you want!!!");
            }
        }

        return base.ProcessBlock(block, blockTracer, options);
    }
}
