// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.Forks;
using Nethermind.State;

namespace Nethermind.Consensus.Bor;

public class BorBlockProcessor : BlockProcessor
{
    private readonly IBorValidatorSetManager _validatorSetManager;
    private readonly IBorStateSyncManager _stateSyncManager;

    private readonly ISpecProvider _specProvider;

    public BorBlockProcessor(
        // Bor specific stuff
        IBorValidatorSetManager validatorSetManager,
        IBorStateSyncManager stateSyncManager,

        // Base BlockProcessor stuff
        ISpecProvider? specProvider,
        IBlockValidator? blockValidator,
        IRewardCalculator? rewardCalculator,
        IBlockProcessor.IBlockTransactionsExecutor? blockTransactionsExecutor,
        IStateProvider? stateProvider,
        IStorageProvider? storageProvider,
        IReceiptStorage? receiptStorage,
        IWitnessCollector? witnessCollector,
        ILogManager? logManager) : base(
            specProvider,
            blockValidator,
            rewardCalculator,
            blockTransactionsExecutor,
            stateProvider,
            storageProvider,
            receiptStorage,
            witnessCollector,
            logManager)
    {
        _validatorSetManager = validatorSetManager;
        _stateSyncManager = stateSyncManager;

        _specProvider = specProvider;
    }

    protected override TxReceipt[] ProcessBlock(Block block, IBlockTracer blockTracer, ProcessingOptions options)
    {
        if (block.IsGenesis)
            return base.ProcessBlock(block, blockTracer, options);

        BorBlockTransferLogsTracer transferLogsTracer = new(_stateProvider);

        CompositeBlockTracer tracer = new();
        tracer.Add(transferLogsTracer);
        tracer.Add(blockTracer);

        TxReceipt[] receipts = base.ProcessBlock(block, tracer, options);

        for (int i = 0, k = 0; i < receipts.Length; i++)
        {
            if (receipts[i].StatusCode == StatusCode.Failure)
                continue;

            LogEntry[] receiptsWithTransferLogs = transferLogsTracer.Logs[k++];
            Bloom bloomWithTransferLogs = new(receiptsWithTransferLogs);

            receipts[i].Logs = receiptsWithTransferLogs;
            receipts[i].Bloom = bloomWithTransferLogs;

            block.Header.Bloom!.Accumulate(bloomWithTransferLogs);
        }

        block.Header.ReceiptsRoot =
            receipts.GetReceiptsRoot(_specProvider.GetSpec(block.Header), null!);

        _validatorSetManager.ProcessBlock(block.Header);
        _stateSyncManager.ProcessBlock(block.Header);

        _stateProvider.RecalculateStateRoot();
        block.Header.StateRoot = _stateProvider.StateRoot;
        block.Header.Hash = block.Header.CalculateHash();

        return receipts;
    }
}