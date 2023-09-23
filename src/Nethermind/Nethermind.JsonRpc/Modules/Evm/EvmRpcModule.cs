// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.JsonRpc.Modules.Evm
{
    public class EvmRpcModule : IEvmRpcModule
    {

        private readonly IManualBlockProductionTrigger _trigger;
        private readonly ITransactionProcessor _txProcessor;
        private readonly IBlockTree _tree;
        private readonly IWorldState _state;


        private Snapshot _snapshot;
        public EvmRpcModule(IManualBlockProductionTrigger? trigger, IBlockTree blockTree, IWorldState? worldState, ITransactionProcessor txProcessor)
        {
            _trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            _txProcessor = txProcessor ?? throw new ArgumentNullException(nameof(txProcessor));
            _tree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
            _state = worldState ?? throw new ArgumentNullException(nameof(worldState));
        }

        public ResultWrapper<bool> evm_mine()
        {
            _trigger.BuildBlock();
            return ResultWrapper<bool>.Success(true);
        }

        public ResultWrapper<GethLikeTxTrace> evm_runBytecode(byte[] input, long gas, long blockNbr, UInt256 timestamp, PresetAddress[] presetAccounts = null)
        {
            _state.Restore(_snapshot);

            foreach (var account in presetAccounts)
            {
                _state.CreateAccount(account.Address, account.Balance, account.Nonce);
            }

            BlockExecutionContext blkCtx = new BlockExecutionContext(_tree.Head.Header);
            Transaction tx = new Transaction
            {
                Data = input,
                GasLimit = gas,
                Timestamp = timestamp
            };

            GethLikeTxMemoryTracer tracer = new(GethTraceOptions.Default);

            _txProcessor.Execute(tx, blkCtx, tracer);

            return ResultWrapper<GethLikeTxTrace>.Success(tracer.BuildResult());
        }

        public void evm_takeSnapshot()
        {
            _snapshot = _state.TakeSnapshot();
        }
    }
}
