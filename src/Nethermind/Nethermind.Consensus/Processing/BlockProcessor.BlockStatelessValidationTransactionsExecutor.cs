// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Verkle.Curve;

namespace Nethermind.Consensus.Processing;

public partial class BlockProcessor
{
    public class BlockStatelessValidationTransactionsExecutor : IBlockProcessor.IBlockTransactionsExecutor
    {
        private readonly ITransactionProcessorAdapter _transactionProcessor;

        public BlockStatelessValidationTransactionsExecutor(ITransactionProcessor transactionProcessor)
            : this(new ExecuteTransactionProcessorAdapter(transactionProcessor))
        {
        }

        public BlockStatelessValidationTransactionsExecutor(ITransactionProcessorAdapter transactionProcessor)
        {
            _transactionProcessor = transactionProcessor;
        }

        public event EventHandler<TxProcessedEventArgs>? TransactionProcessed;

        public TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions, BlockReceiptsTracer receiptsTracer, IReleaseSpec spec)
        {
            for (int i = 0; i < block.Transactions.Length; i++)
            {
                Transaction currentTx = block.Transactions[i];
                ProcessTransaction(block, currentTx, i, receiptsTracer, processingOptions);
            }
            return receiptsTracer.TxReceipts.ToArray();
        }

        private void ProcessTransaction(Block block, Transaction currentTx, int index, BlockReceiptsTracer receiptsTracer, ProcessingOptions processingOptions)
        {
            block.Header.MaybeParent.TryGetTarget(out BlockHeader maybeParent);
            VerkleWorldState worldState = new(block.Header.Witness!.Value, Banderwagon.FromBytes(maybeParent.StateRoot.Bytes)!.Value, LimboLogs.Instance);
            _transactionProcessor.ProcessTransaction(block, currentTx, receiptsTracer, processingOptions, worldState);
            TransactionProcessed?.Invoke(this, new TxProcessedEventArgs(index, currentTx, receiptsTracer.TxReceipts[index]));
        }
    }
}
