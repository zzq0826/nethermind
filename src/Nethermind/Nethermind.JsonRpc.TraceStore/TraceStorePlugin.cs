// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Evm.Tracing.ParityStyle;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Trace;
using Nethermind.Logging;

namespace Nethermind.JsonRpc.TraceStore;

public class TraceStorePlugin : INethermindPlugin
{
    private const string DbName = "TraceStore";
    private static readonly Keccak _modeDbKey = Keccak.Zero;
    private INethermindApi _api = null!;
    private ITraceStoreConfig _config = null!;
    private IJsonRpcConfig _jsonRpcConfig = null!;
    private IDbWithSpan? _db;
    private TraceStorePruner? _pruner;
    private ILogManager _logManager = null!;
    private ILogger _logger = null!;
    private ITraceSerializer<ParityLikeTxTrace>? _traceSerializer;
    private ITraceSerializer<ParityTxTraceFromStore>? _traceStoreSerializer;
    public string Name => DbName;
    public string Description => "Allows to serve traces without the block state, by saving historical traces to DB.";
    public string Author => "Nethermind";
    private bool Enabled => _config.Enabled;

    public Task Init(INethermindApi nethermindApi)
    {
        _api = nethermindApi;
        _logManager = _api.LogManager;
        _config = _api.Config<ITraceStoreConfig>();
        _jsonRpcConfig = _api.Config<IJsonRpcConfig>();
        _logger = _logManager.GetClassLogger<TraceStorePlugin>();

        if (Enabled)
        {
            // Setup DB
            _db = (IDbWithSpan)_api.RocksDbFactory!.CreateDb(new RocksDbSettings(DbName, DbName.ToLower()));
            _api.DbProvider!.RegisterDb(DbName, _db);
            InitMode();

            // Setup serialization
            _traceSerializer = new ParityLikeTraceSerializer(_logManager, _config.MaxDepth, _config.VerifySerialized);
            _traceStoreSerializer = new ParityTxTraceFromStoreSerializer();

            //Setup pruning if configured
            if (_config.BlocksToKeep != 0)
            {
                _pruner = new TraceStorePruner(_api.BlockTree!, _db, _config.BlocksToKeep, _logManager);
            }
        }

        return Task.CompletedTask;
    }

    private void InitMode()
    {
        byte[]? bytes = _db![_modeDbKey.Bytes];
        TraceStoreMode mode = bytes is null || bytes.Length == 0 ? TraceStoreMode.Default : (TraceStoreMode)bytes[0];
        if (mode == TraceStoreMode.Default)
        {
            _db[_modeDbKey.Bytes] = new[] { (byte)_config.Mode };
        }
        else if (_config.Mode != mode)
        {
            if (_logger.IsWarn) _logger.Warn($"TraceStore mode is set to '{_config.Mode}' but database was created with '{mode}' mode. Reverting to '{mode}' mode. If you want to change the mode you have to resync the node.");
            _config.Mode = mode;
        }
    }

    public Task InitNetworkProtocol()
    {
        if (Enabled)
        {
            if (_logger.IsInfo) _logger.Info($"Starting TraceStore with {_config.TraceTypes} traces.");

            // Setup tracing
            ParityLikeBlockTracer parityTracer = new(_config.TraceTypes);
            ITraceSerializer<ParityLikeTxTrace>? serializer = _config.Mode == TraceStoreMode.Complex
                ? _traceSerializer!
                : new ParityTxTraceFromStoreSerializerAdapter(_traceStoreSerializer!);

            DbPersistingBlockTracer<ParityLikeTxTrace, ParityLikeTxTracer> dbPersistingTracer = new(parityTracer, _db!, serializer, _logManager);
            _api.BlockchainProcessor!.Tracers.Add(dbPersistingTracer);
        }

        // Potentially we could add protocol for syncing traces.
        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        if (Enabled && _jsonRpcConfig.Enabled)
        {
            IRpcModuleProvider apiRpcModuleProvider = _api.RpcModuleProvider!;
            if (apiRpcModuleProvider.GetPool(ModuleType.Trace) is IRpcModulePool<ITraceRpcModule> traceModulePool)
            {
                TraceStoreModuleFactory traceModuleFactory = new(traceModulePool.Factory, _db!, _api.BlockTree!, _api.ReceiptFinder!, _traceSerializer!, _traceStoreSerializer, _logManager, _config.Mode);
                apiRpcModuleProvider.RegisterBoundedByCpuCount(traceModuleFactory, _jsonRpcConfig.Timeout);
            }
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (Enabled)
        {
            _pruner?.Dispose();
            _db?.Dispose();
        }

        return default;
    }
}
