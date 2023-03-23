using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Transactions;

namespace Nethermind.Consensus.Bor;

public class BorPlugin : IConsensusPlugin
{
    public string Name => "Bor";

    public string Description => $"{SealEngineType} Consensus Engine";

    public string Author => "Nethermind";

    public string SealEngineType => "Bor";

    public Task Init(INethermindApi api)
    {
        if (api.SealEngineType != SealEngineType)
            return Task.CompletedTask;

        (IApiWithFactories getApi, IApiWithBlockchain setApi) = api.ForInit;

        setApi.BlockProcessorFactory = new BorBlockProcessorFactory(api);

        // We need to validate that the sealer is the correct one
        setApi.SealValidator = new BorSealValidator();

        // There are no block rewards on the Bor layer
        setApi.RewardCalculatorSource = NoBlockRewards.Instance;

        // taken from clique, but will probably also need to do it here, we'll see
        // setApi.BlockPreprocessor.AddLast(new AuthorRecoveryStep(_snapshotManager!));

        return Task.CompletedTask;
    }

    public IBlockProductionTrigger DefaultBlockProductionTrigger => throw new NotImplementedException();

    public Task<IBlockProducer> InitBlockProducer(IBlockProductionTrigger? blockProductionTrigger = null, ITxSource? additionalTxSource = null)
    {
        throw new NotImplementedException();
    }

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }

    public Task InitRpcModules()
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}