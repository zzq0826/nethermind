// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.State;

namespace Nethermind.Consensus.Processing
{
    public partial class BlockProcessor
    {
        public class BlockValidationTransactionsExecutor : IBlockProcessor.IBlockTransactionsExecutor
        {
            private readonly ITransactionProcessorAdapter _transactionProcessor;
            private readonly IWorldState _stateProvider;

            public BlockValidationTransactionsExecutor(ITransactionProcessor transactionProcessor, IWorldState stateProvider)
                : this(new ExecuteTransactionProcessorAdapter(transactionProcessor), stateProvider)
            {
            }

            public BlockValidationTransactionsExecutor(ITransactionProcessorAdapter transactionProcessor, IWorldState stateProvider)
            {
                _transactionProcessor = transactionProcessor;
                _stateProvider = stateProvider;
            }

            public event EventHandler<TxProcessedEventArgs>? TransactionProcessed;

            public TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions, BlockReceiptsTracer receiptsTracer, IReleaseSpec spec)
            {
                //try
                //{
                    Evm.Metrics.ResetBlockStats();
                    BlockExecutionContext blkCtx = new(block.Header);
                    var txs = block.Transactions;

                    IVirtualMachine virtualMachine = _transactionProcessor.VirtualMachine;
                    IEnumerable<(Address address, bool eao)> addresses = txs.Select(tx => (address: tx.SenderAddress, eao: true))
                        .Concat(txs.Select(t => (address: t.To, eao: false)))
                        .Concat(txs.Where(tx => tx.AccessList != null).SelectMany(tx => tx.AccessList).Select(al => (address: al.Address, eao: false)))
                        .Where(a => a.Item1 != null)
                        .OrderBy(a => a.Item1)
                        .DistinctBy(a => a.Item1);

                    Parallel.ForEach(addresses, (data) =>
                    {
                        _stateProvider.CacheFromTree(data.address);
                        if (!data.eao)
                        {
                            virtualMachine.CacheCodeInfo(_stateProvider, data.address, spec);
                        }
                    });

                    for (int i = 0; i < txs.Length; i++)
                    {
                        Transaction currentTx = txs[i];
                        ProcessTransaction(in blkCtx, currentTx, i, receiptsTracer, processingOptions);
                    }

                    return receiptsTracer.TxReceipts.ToArray();
                //}
                //finally
                //{
                //    _stateProvider.ResetBaseCache();
                //}
            }

            private void ProcessTransaction(in BlockExecutionContext blkCtx, Transaction currentTx, int index, BlockReceiptsTracer receiptsTracer, ProcessingOptions processingOptions)
            {
                _transactionProcessor.ProcessTransaction(in blkCtx, currentTx, receiptsTracer, processingOptions, _stateProvider);
                TransactionProcessed?.Invoke(this, new TxProcessedEventArgs(index, currentTx, receiptsTracer.TxReceipts[index]));
            }
        }
    }
}
