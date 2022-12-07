// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Evm.Tracing.ParityStyle;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.Logging;

namespace Nethermind.JsonRpc.TraceStore;

/// <summary>
/// Module for tracing using database
/// </summary>
public class TraceStoreRpcModule : ITraceRpcModule
{
    private readonly IDbWithSpan _traceStore;
    private readonly ITraceRpcModule _traceModule;
    private readonly IBlockFinder _blockFinder;
    private readonly IReceiptFinder _receiptFinder;
    private readonly ITraceSerializer<ParityLikeTxTrace> _traceSerializer;
    private readonly ITraceSerializer<ParityTxTraceFromStore> _traceStoreSerializer;
    private readonly TraceStoreMode _mode;
    private readonly ILogger _logger;

    private static readonly IDictionary<ParityTraceTypes, Action<ParityLikeTxTrace>> _filters = new Dictionary<ParityTraceTypes, Action<ParityLikeTxTrace>>
    {
        { ParityTraceTypes.Trace, FilterTrace },
        { ParityTraceTypes.StateDiff , FilterStateDiff },
        { ParityTraceTypes.VmTrace | ParityTraceTypes.Trace, FilterStateVmTrace }
    };

    public TraceStoreRpcModule(ITraceRpcModule traceModule,
        IDbWithSpan traceStore,
        IBlockFinder blockFinder,
        IReceiptFinder receiptFinder,
        ITraceSerializer<ParityLikeTxTrace> traceSerializer,
        ITraceSerializer<ParityTxTraceFromStore> traceStoreSerializer,
        ILogManager logManager,
        TraceStoreMode mode)
    {
        _traceStore = traceStore;
        _traceModule = traceModule;
        _blockFinder = blockFinder;
        _receiptFinder = receiptFinder;
        _traceSerializer = traceSerializer;
        _traceStoreSerializer = traceStoreSerializer;
        _mode = mode;
        _logger = logManager.GetClassLogger<TraceStoreRpcModule>();
    }

    public ResultWrapper<ParityTxTraceFromReplay> trace_call(TransactionForRpc call, string[] traceTypes, BlockParameter? blockParameter = null) =>
        _traceModule.trace_call(call, traceTypes, blockParameter);

    public ResultWrapper<IEnumerable<ParityTxTraceFromReplay>> trace_callMany(TransactionForRpcWithTraceTypes[] calls, BlockParameter? blockParameter = null) =>
        _traceModule.trace_callMany(calls, blockParameter);

    public ResultWrapper<ParityTxTraceFromReplay> trace_rawTransaction(byte[] data, string[] traceTypes) =>
        _traceModule.trace_rawTransaction(data, traceTypes);

    public ResultWrapper<ParityTxTraceFromReplay> trace_replayTransaction(Keccak txHash, params string[] traceTypes)
    {
        if (_mode == TraceStoreMode.Complex)
        {
            SearchResult<Keccak> blockHashSearch = _receiptFinder.SearchForReceiptBlockHash(txHash);
            if (blockHashSearch.IsError)
            {
                return ResultWrapper<ParityTxTraceFromReplay>.Fail(blockHashSearch);
            }

            SearchResult<Block> blockSearch = _blockFinder.SearchForBlock(new BlockParameter(blockHashSearch.Object!));
            if (blockSearch.IsError)
            {
                return ResultWrapper<ParityTxTraceFromReplay>.Fail(blockSearch);;
            }

            Block block = blockSearch.Object!;

            if (TryGetBlockTraces(block.Header, out List<ParityLikeTxTrace>? traces) && traces is not null)
            {
                ParityLikeTxTrace? trace = GetTxTrace(txHash, traces);
                if (trace is not null)
                {
                    FilterTrace(trace, TraceRpcModule.GetParityTypes(traceTypes));
                    return ResultWrapper<ParityTxTraceFromReplay>.Success(new ParityTxTraceFromReplay(trace));
                }
            }
        }

        return _traceModule.trace_replayTransaction(txHash, traceTypes);
    }

    public ResultWrapper<IEnumerable<ParityTxTraceFromReplay>> trace_replayBlockTransactions(BlockParameter blockParameter, string[] traceTypes)
    {
        if (_mode == TraceStoreMode.Complex)
        {
            SearchResult<BlockHeader> blockSearch = _blockFinder.SearchForHeader(blockParameter);
            if (blockSearch.IsError)
            {
                return ResultWrapper<IEnumerable<ParityTxTraceFromReplay>>.Fail(blockSearch);
            }

            BlockHeader block = blockSearch.Object!;

            if (TryGetBlockTraces(block, out List<ParityLikeTxTrace>? traces) && traces is not null)
            {
                FilterTraces(traces, TraceRpcModule.GetParityTypes(traceTypes));
                return ResultWrapper<IEnumerable<ParityTxTraceFromReplay>>.Success(traces.Select(t => new ParityTxTraceFromReplay(t, true)));
            }
        }

        return _traceModule.trace_replayBlockTransactions(blockParameter, traceTypes);
    }

    public ResultWrapper<IEnumerable<ParityTxTraceFromStore>> trace_filter(TraceFilterForRpc traceFilterForRpc)
    {
        IEnumerable<SearchResult<Block>> blocksSearch = _blockFinder.SearchForBlocksOnMainChain(
            traceFilterForRpc.FromBlock ?? BlockParameter.Latest,
            traceFilterForRpc.ToBlock ?? BlockParameter.Latest);

        SearchResult<Block>? error = null;
        bool missingTraces = false;

        IEnumerable<ParityTxTraceFromStore> txTraces = blocksSearch.AsParallel()
            .AsOrdered()
            .SelectMany(blockSearch =>
            {
                if (blockSearch.IsError)
                {
                    error = blockSearch;
                    return Enumerable.Empty<ParityTxTraceFromStore>();
                }

                Block block = blockSearch.Object!;
                if (TryGetBlockTraces(block.Header, out IEnumerable<ParityTxTraceFromStore>? traces) && traces is not null)
                {
                    return traces;
                }
                else
                {
                    missingTraces = true;
                    return Enumerable.Empty<ParityTxTraceFromStore>();
                }
            });

        if (error is not null)
        {
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Fail(error.Value);
        }
        else if (missingTraces)
        {
            // fallback when we miss any traces in db
            return _traceModule.trace_filter(traceFilterForRpc);
        }
        else
        {
            TxTraceFilter txTracerFilter = new(traceFilterForRpc.FromAddress, traceFilterForRpc.ToAddress, traceFilterForRpc.After, traceFilterForRpc.Count);
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Success(txTracerFilter.FilterTxTraces(txTraces));
        }
    }

    public ResultWrapper<IEnumerable<ParityTxTraceFromStore>> trace_block(BlockParameter blockParameter)
    {
        SearchResult<BlockHeader> blockSearch = _blockFinder.SearchForHeader(blockParameter);
        if (blockSearch.IsError)
        {
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Fail(blockSearch);
        }

        BlockHeader block = blockSearch.Object!;
        if (TryGetBlockTraces(block, out IEnumerable<ParityTxTraceFromStore>? traces) && traces is not null)
        {
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Success(traces);
        }

        return _traceModule.trace_block(blockParameter);
    }

    public ResultWrapper<IEnumerable<ParityTxTraceFromStore>> trace_get(Keccak txHash, long[] positions)
    {
        ResultWrapper<IEnumerable<ParityTxTraceFromStore>> traceTransaction = trace_transaction(txHash);
        List<ParityTxTraceFromStore> traces = TraceRpcModule.ExtractPositionsFromTxTrace(positions, traceTransaction);
        return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Success(traces);
    }

    public ResultWrapper<IEnumerable<ParityTxTraceFromStore>> trace_transaction(Keccak txHash)
    {
        SearchResult<Keccak> blockHashSearch = _receiptFinder.SearchForReceiptBlockHash(txHash);
        if (blockHashSearch.IsError)
        {
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Fail(blockHashSearch);
        }

        SearchResult<Block> blockSearch = _blockFinder.SearchForBlock(new BlockParameter(blockHashSearch.Object!));
        if (blockSearch.IsError)
        {
            return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Fail(blockSearch);
        }

        Block block = blockSearch.Object!;
        if (_mode == TraceStoreMode.Complex)
        {
            if (TryGetBlockTraces(block.Header, out List<ParityLikeTxTrace>? traces) && traces is not null)
            {
                ParityLikeTxTrace? trace = GetTxTrace(txHash, traces);
                if (trace is not null)
                {
                    FilterTrace(trace, ParityTraceTypes.Trace);
                    return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Success(ParityTxTraceFromStore.FromTxTrace(trace));
                }
            }
        }
        else
        {
            if (TryGetBlockTraces(block.Header, out IEnumerable<ParityTxTraceFromStore>? traces) && traces is not null)
            {
                return ResultWrapper<IEnumerable<ParityTxTraceFromStore>>.Success(traces.Where(t => t.TransactionHash == txHash));
            }
        }

        return _traceModule.trace_transaction(txHash);
    }

    private bool TryGetBlockTraces(BlockHeader block, out IEnumerable<ParityTxTraceFromStore>? traces)
    {
        if (_mode == TraceStoreMode.Complex)
        {
            bool tryGetTraces = TryGetBlockTraces(block, out List<ParityLikeTxTrace>? parityTraces);
            traces = parityTraces?.SelectMany(ParityTxTraceFromStore.FromTxTrace);
            return tryGetTraces;
        }
        else
        {
            bool tryGetTraces = TryGetBlockTraces(block, _traceStoreSerializer, out List<ParityTxTraceFromStore>? storeTraces);
            traces = storeTraces;
            return tryGetTraces;
        }
    }

    private bool TryGetBlockTraces(BlockHeader block, out List<ParityLikeTxTrace>? traces) =>
        TryGetBlockTraces(block, _traceSerializer, out traces);

    private bool TryGetBlockTraces<TTrace>(BlockHeader block, ITraceSerializer<TTrace> serializer, out List<TTrace>? traces)
    {
        Span<byte> tracesSerialized = _traceStore.GetSpan(block.Hash!);
        try
        {
            if (!tracesSerialized.IsEmpty)
            {
                List<TTrace>? tracesDeserialized = serializer.Deserialize(tracesSerialized);
                if (tracesDeserialized is not null)
                {
                    if (_logger.IsTrace) _logger.Trace($"Found persisted traces for block {block.ToString(BlockHeader.Format.FullHashAndNumber)}");
                    traces = tracesDeserialized;
                    return true;
                }
            }

            traces = null;
            return false;
        }
        finally
        {
            _traceStore.DangerousReleaseMemory(tracesSerialized);
        }
    }

    private ParityLikeTxTrace? GetTxTrace(Keccak txHash, List<ParityLikeTxTrace> traces)
    {
        int index = traces.FindIndex(t => t.TransactionHash == txHash);
        return index != -1 ? traces[index] : null;
    }

    private void FilterTraces(List<ParityLikeTxTrace> traces, ParityTraceTypes traceTypes)
    {
        for (int i = 0; i < traces.Count; i++)
        {
            ParityLikeTxTrace parityLikeTxTrace = traces[i];
            FilterTrace(parityLikeTxTrace, traceTypes);
        }

        if ((traceTypes & ParityTraceTypes.Rewards) == 0)
        {
            FilterRewards(traces);
        }
    }

    private static void FilterTrace(ParityLikeTxTrace parityLikeTxTrace, ParityTraceTypes traceTypes)
    {
        foreach (KeyValuePair<ParityTraceTypes, Action<ParityLikeTxTrace>> filter in _filters)
        {
            if ((traceTypes & filter.Key) == 0)
            {
                filter.Value(parityLikeTxTrace);
            }
        }
    }


    // VmTrace uses flags IsTracingCode, IsTracingInstructions
    private static void FilterStateVmTrace(ParityLikeTxTrace trace)
    {
        trace.VmTrace = null;
    }

    // StateDiff uses flags IsTracingState, IsTracingStorage
    private static void FilterStateDiff(ParityLikeTxTrace trace)
    {
        trace.StateChanges = null;
        if (trace.VmTrace is not null)
        {
            for (int i = 0; i < trace.VmTrace.Operations.Length; i++)
            {
                trace.VmTrace.Operations[i].Store = null!;
            }
        }
    }

    // Trace uses flags IsTracingActions, IsTracingReceipt
    private static void FilterTrace(ParityLikeTxTrace trace)
    {
        trace.Output = null;
        // trace action?
    }

    private static void FilterRewards(List<ParityLikeTxTrace> traces)
    {
        for (int i = traces.Count - 1; i >= 0; i--)
        {
            ParityLikeTxTrace trace = traces[i];
            if (trace.TransactionHash is null && trace.Action?.Type == "reward")
            {
                traces.RemoveAt(i);
            }
            else
            {
                break;
            }
        }
    }
}
