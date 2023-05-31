// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.Tracing.DebugTrace;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Consensus.Tracing
{
    public class DebugStyleTracer : IDebugStyleTracer
    {
        private readonly IBlockTree _blockTree;
        private readonly ChangeableTransactionProcessorAdapter _transactionProcessorAdapter;
        private readonly IBlockchainProcessor _processor;
        private readonly IReceiptStorage _receiptStorage;

        public DebugStyleTracer(
            IBlockchainProcessor processor,
            IReceiptStorage receiptStorage,
            IBlockTree blockTree,
            ChangeableTransactionProcessorAdapter transactionProcessorAdapter)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _receiptStorage = receiptStorage ?? throw new ArgumentNullException(nameof(receiptStorage));
            _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
            _transactionProcessorAdapter = transactionProcessorAdapter;
        }

        public GethLikeTxTrace Trace(Keccak blockHash, int txIndex, CancellationToken cancellationToken)
        {
            Block block = _blockTree.FindBlock(blockHash, BlockTreeLookupOptions.None);
            if (block is null) throw new InvalidOperationException("Only historical blocks");

            if (txIndex > block.Transactions.Length - 1) throw new InvalidOperationException($"Block {blockHash} has only {block.Transactions.Length} transactions and the requested tx index was {txIndex}");

            return Trace(block, block.Transactions[txIndex].Hash, cancellationToken);
        }

        public GethLikeTxTrace? Trace(Rlp block, Keccak txHash, CancellationToken cancellationToken)
        {
            return TraceBlock(GetBlockToTrace(block), cancellationToken, txHash).FirstOrDefault();
        }

        public GethLikeTxTrace? Trace(BlockParameter blockParameter, Transaction tx, CancellationToken cancellationToken)
        {
            Block block = _blockTree.FindBlock(blockParameter);
            if (block is null) throw new InvalidOperationException($"Cannot find block {blockParameter}");
            tx.Hash ??= tx.CalculateHash();
            block = block.WithReplacedBodyCloned(BlockBody.WithOneTransactionOnly(tx));
            ITransactionProcessorAdapter currentAdapter = _transactionProcessorAdapter.CurrentAdapter;
            _transactionProcessorAdapter.CurrentAdapter = new TraceTransactionProcessorAdapter(_transactionProcessorAdapter.TransactionProcessor);

            try
            {
                return Trace(block, tx.Hash, cancellationToken);
            }
            finally
            {
                _transactionProcessorAdapter.CurrentAdapter = currentAdapter;
            }
        }

        public GethLikeTxTrace? Trace(Keccak txHash, CancellationToken cancellationToken)
        {
            Keccak? blockHash = _receiptStorage.FindBlockHash(txHash);
            if (blockHash is null)
            {
                return null;
            }

            Block block = _blockTree.FindBlock(blockHash, BlockTreeLookupOptions.RequireCanonical);
            if (block is null)
            {
                return null;
            }

            return Trace(block, txHash, cancellationToken);
        }

        public GethLikeTxTrace? Trace(long blockNumber, int txIndex, CancellationToken cancellationToken)
        {
            Block block = _blockTree.FindBlock(blockNumber, BlockTreeLookupOptions.RequireCanonical);
            if (block is null) throw new InvalidOperationException("Only historical blocks");

            if (txIndex > block.Transactions.Length - 1) throw new InvalidOperationException($"Block {blockNumber} has only {block.Transactions.Length} transactions and the requested tx index was {txIndex}");

            return Trace(block, block.Transactions[txIndex].Hash, cancellationToken);
        }

        public GethLikeTxTrace? Trace(long blockNumber, Transaction tx, CancellationToken cancellationToken)
        {
            Block block = _blockTree.FindBlock(blockNumber, BlockTreeLookupOptions.RequireCanonical);
            if (block is null) throw new InvalidOperationException("Only historical blocks");
            if (tx.Hash is null) throw new InvalidOperationException("Cannot trace transactions without tx hash set.");

            block = block.WithReplacedBodyCloned(BlockBody.WithOneTransactionOnly(tx));
            DebugBlockTracer blockTracer = new DebugBlockTracer(tx.Hash);

            Thread _workerThread = new(() =>
            {
                try
                {
                    _processor.Process(block, ProcessingOptions.Trace, blockTracer.WithCancellation(cancellationToken));
                } catch (ThreadInterruptedException) {
                }
            });

            while (_workerThread.IsAlive)
            {
                if (blockTracer.IsBlocking)
                {
                    Console.ReadKey();
                    blockTracer.MoveNext();
                }
            }

            return blockTracer.BuildResult().SingleOrDefault();
        }
        public GethLikeTxTrace[] TraceBlock(BlockParameter blockParameter,CancellationToken cancellationToken)
        {
            var block = _blockTree.FindBlock(blockParameter);

            return TraceBlock(block, cancellationToken);
        }

        public GethLikeTxTrace[] TraceBlock(Rlp blockRlp, CancellationToken cancellationToken)
        {
            return TraceBlock(GetBlockToTrace(blockRlp), cancellationToken);
        }

        private GethLikeTxTrace? Trace(Block block, Keccak? txHash, CancellationToken cancellationToken)
        {
            if (txHash is null) throw new InvalidOperationException("Cannot trace transactions without tx hash set.");

            DebugBlockTracer listener = new(txHash);
            _processor.Process(block, ProcessingOptions.Trace, listener.WithCancellation(cancellationToken));
            return listener.BuildResult().SingleOrDefault();
        }

        private GethLikeTxTrace[] TraceBlock(Block? block, CancellationToken cancellationToken, Keccak? txHash = null)
        {
            if (block is null) throw new InvalidOperationException("Only canonical, historical blocks supported");

            if (!block.IsGenesis)
            {
                BlockHeader? parent = _blockTree.FindParentHeader(block.Header, BlockTreeLookupOptions.None);
                if (parent?.Hash is null)
                {
                    throw new InvalidOperationException("Cannot trace blocks with invalid parents");
                }

                if (!_blockTree.IsMainChain(parent.Hash)) throw new InvalidOperationException("Cannot trace orphaned blocks");
            }

            DebugBlockTracer listener = new DebugBlockTracer(txHash);
            _processor.Process(block, ProcessingOptions.Trace, listener.WithCancellation(cancellationToken));
            return listener.BuildResult().ToArray();
        }

        private static Block GetBlockToTrace(Rlp blockRlp)
        {
            Block block = Rlp.Decode<Block>(blockRlp);
            if (block.TotalDifficulty is null)
            {
                block.Header.TotalDifficulty = 1;
            }

            return block;
        }
    }
}
