// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
using Nethermind.Blockchain.Find;
using Nethermind.Config;
using Nethermind.Consensus.Tracing;
using Nethermind.Core;
using Nethermind.Facade;
using Nethermind.Facade.Proxy.Models.MultiCall;

namespace Nethermind.JsonRpc.Modules.Eth;

public abstract class ExecutorBase<TResult, TRequest, TProcessing>
{
    private readonly IBlockFinder _blockFinder;
    protected readonly IBlockchainBridge _blockchainBridge;
    protected readonly IJsonRpcConfig _rpcConfig;
    private readonly IBlocksConfig _blocksConfig;

    protected ExecutorBase(IBlockchainBridge blockchainBridge, IBlockFinder blockFinder, IJsonRpcConfig rpcConfig, IBlocksConfig blocksConfig)
    {
        _blockchainBridge = blockchainBridge;
        _blockFinder = blockFinder;
        _rpcConfig = rpcConfig;
        _blocksConfig = blocksConfig;
    }

    public virtual ResultWrapper<TResult> Execute(
        TRequest call,
        BlockParameter? blockParameter,
        BlockOverride? blockOverride)
    {
        SearchResult<BlockHeader> searchResult = _blockFinder.SearchForHeader(blockParameter);
        if (searchResult.IsError)
        {
            return ResultWrapper<TResult>.Fail(searchResult);
        }

        BlockHeader header = searchResult.Object;
        if (!_blockchainBridge.HasStateForBlock(header!))
        {
            return ResultWrapper<TResult>.Fail($"No state available for block {header.Hash}", ErrorCodes.ResourceUnavailable);
        }

        using CancellationTokenSource cancellationTokenSource = new(_rpcConfig.Timeout);
        TProcessing? toProcess = Prepare(call);
        return Execute(blockOverride?.GetBlockHeader(header, _blocksConfig) ?? header.Clone(), toProcess, cancellationTokenSource.Token);
    }

    protected abstract TProcessing Prepare(TRequest call);

    protected abstract ResultWrapper<TResult> Execute(BlockHeader header, TProcessing tx, CancellationToken token);

    protected ResultWrapper<TResult>? TryGetInputError(CallOutput result) =>
        result.InputError ? ResultWrapper<TResult>.Fail(result.Error!, ErrorCodes.InvalidInput) : null;
}
