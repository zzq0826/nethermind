// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml;
using Nethermind.Blockchain.Find;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Tracing;
using Nethermind.Core;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Facade;
using Nethermind.Facade.Proxy.Models;
using Nethermind.Facade.Proxy.Models.MultiCall;
using Nethermind.Int256;
using Nethermind.JsonRpc.Data;
using Nethermind.Specs.Forks;

namespace Nethermind.JsonRpc.Modules.Eth
{
    //General executor
    public partial class EthRpcModule
    {
        // Single call executor
        private abstract class TxExecutor<TResult> : ExecutorBase<TResult, TransactionForRpc, Transaction>
        {
            protected TxExecutor(IBlockchainBridge blockchainBridge, IBlockFinder blockFinder, IJsonRpcConfig rpcConfig, IBlocksConfig blocksConfig)
                : base(blockchainBridge, blockFinder, rpcConfig, blocksConfig) { }

            protected override Transaction Prepare(TransactionForRpc call) => call.ToTransaction(_blockchainBridge.GetChainId());

            protected override ResultWrapper<TResult> Execute(BlockHeader header, Transaction tx, CancellationToken token) => ExecuteTx(header, tx, token);

            public override ResultWrapper<TResult> Execute(
                TransactionForRpc transactionCall,
                BlockParameter? blockParameter,
                BlockOverride? blockOverride)
            {
                transactionCall.EnsureDefaults(_rpcConfig.GasCap);
                return base.Execute(transactionCall, blockParameter, blockOverride);
            }

            public ResultWrapper<TResult> ExecuteTx(TransactionForRpc transactionCall, BlockParameter? blockParameter, BlockOverride? blockOverride = null) =>
                Execute(transactionCall, blockParameter, blockOverride);

            protected abstract ResultWrapper<TResult> ExecuteTx(BlockHeader header, Transaction tx, CancellationToken token);
        }

        private class CallTxExecutor : TxExecutor<string>
        {
            private readonly Dictionary<Address, AccountOverride>? _accountOverrides;

            public CallTxExecutor(
                IBlockchainBridge blockchainBridge,
                IBlockFinder blockFinder,
                IJsonRpcConfig rpcConfig,
                IBlocksConfig blocksConfig,
                Dictionary<Address, AccountOverride>? accountOverrides = null)
                : base(blockchainBridge, blockFinder, rpcConfig, blocksConfig)
            {
                _accountOverrides = accountOverrides;
            }

            protected override ResultWrapper<string> ExecuteTx(BlockHeader header, Transaction tx, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.Call(header, tx, token, _accountOverrides);

                return result.Error is null
                    ? ResultWrapper<string>.Success(result.OutputData.ToHexString(true))
                    : TryGetInputError(result) ?? ResultWrapper<string>.Fail("VM execution error.", ErrorCodes.ExecutionError, result.Error);
            }

        }

        private class EstimateGasTxExecutor : TxExecutor<UInt256?>
        {
            public EstimateGasTxExecutor(IBlockchainBridge blockchainBridge, IBlockFinder blockFinder, IJsonRpcConfig rpcConfig, IBlocksConfig blocksConfig)
                : base(blockchainBridge, blockFinder, rpcConfig, blocksConfig)
            {
            }

            protected override ResultWrapper<UInt256?> ExecuteTx(BlockHeader header, Transaction tx, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.EstimateGas(header, tx, token);

                return result.Error is null
                    ? ResultWrapper<UInt256?>.Success((UInt256)result.GasSpent)
                    : TryGetInputError(result) ?? ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.ExecutionError);
            }
        }

        private class CreateAccessListTxExecutor : TxExecutor<AccessListForRpc>
        {
            private readonly bool _optimize;

            public CreateAccessListTxExecutor(IBlockchainBridge blockchainBridge, IBlockFinder blockFinder, IJsonRpcConfig rpcConfig, IBlocksConfig blocksConfig, bool optimize)
                : base(blockchainBridge, blockFinder, rpcConfig, blocksConfig)
            {
                _optimize = optimize;
            }

            protected override ResultWrapper<AccessListForRpc> ExecuteTx(BlockHeader header, Transaction tx, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.CreateAccessList(header, tx, token, _optimize);

                return result.Error is null
                    ? ResultWrapper<AccessListForRpc>.Success(new(GetResultAccessList(tx, result), GetResultGas(tx, result)))
                    : TryGetInputError(result) ?? ResultWrapper<AccessListForRpc>.Fail(result.Error, ErrorCodes.ExecutionError, new AccessListForRpc(GetResultAccessList(tx, result), GetResultGas(tx, result)));
            }


            private static IEnumerable<AccessListItemForRpc> GetResultAccessList(Transaction tx, CallOutput result)
            {
                AccessList? accessList = result.AccessList ?? tx.AccessList;
                return accessList is null ? Enumerable.Empty<AccessListItemForRpc>() : AccessListItemForRpc.FromAccessList(accessList);
            }

            private static UInt256 GetResultGas(Transaction transaction, CallOutput result)
            {
                long gas = result.GasSpent;
                if (result.AccessList is not null)
                {
                    // if we generated access list, we need to fix actual gas cost, as all storage was considered warm
                    gas -= IntrinsicGasCalculator.Calculate(transaction, Berlin.Instance);
                    transaction.AccessList = result.AccessList;
                    gas += IntrinsicGasCalculator.Calculate(transaction, Berlin.Instance);
                }

                return (UInt256)gas;
            }
        }
    }
}
