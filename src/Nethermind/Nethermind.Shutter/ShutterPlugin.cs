// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Consensus;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Shutter;

public sealed class ShutterPlugin : IShutterPlugin
{
    private IShutterConfig _config = null!;
    private ILogger _logger = default!;
    private ILogManager _logManager = default!;
    private INethermindApi _nethermindApi = default!;

    public ValueTask DisposeAsync() => default;

    public Task Init(INethermindApi nethermindApi)
    {
        ArgumentNullException.ThrowIfNull(nethermindApi);

        _nethermindApi = nethermindApi;
        _config = _nethermindApi.Config<IShutterConfig>();
        _logManager = _nethermindApi.LogManager;
        _logger = _logManager.GetClassLogger<ShutterPlugin>();

        return Task.CompletedTask;
    }

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }

    public Task InitRpcModules() => Task.CompletedTask;

    //public Task<IBlockProducer> InitBlockProducer(IConsensusWrapperPlugin consensusPlugin, ITxSource? additionalTxSource = null)
    //{
    //    return consensusPlugin.InitBlockProducer(consensusPlugin, additionalTxSource);
    //}

    public Task<IBlockProducer> InitBlockProducer(IConsensusWrapperPlugin consensusPlugin, IConsensusPlugin consensusP)
    {
        return consensusPlugin.InitBlockProducer(consensusP, new BlkProd());
    }

    public string Author { get; } = "Nethermind";

    public string Description { get; } = "TODO";

    private bool Enabled => true;

    public string Name { get; } = "Shutter";

    //bool IConsensusWrapperPlugin.Enabled => Enabled;

    public class BlkProd : ITxSource
    {
        public IEnumerable<Transaction> GetTransactions(BlockHeader parent, long gasLimit)
        {
            return Enumerable.Empty<Transaction>();
        }
    }
}
