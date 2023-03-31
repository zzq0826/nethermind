// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Factories;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Consensus.Bor;

public class BorBlockProcessorFactory : IApiComponentFactory<IBlockProcessor>
{
    private readonly INethermindApi _api;

    public BorBlockProcessorFactory(INethermindApi api)
    {
        _api = api;
    }

    public IBlockProcessor Create()
    {
        ArgumentNullException.ThrowIfNull(_api.ChainSpec);
        ArgumentNullException.ThrowIfNull(_api.BlockTree);
        ArgumentNullException.ThrowIfNull(_api.DbProvider);
        ArgumentNullException.ThrowIfNull(_api.StateProvider);
        ArgumentNullException.ThrowIfNull(_api.RewardCalculatorSource);
        ArgumentNullException.ThrowIfNull(_api.TransactionProcessor);

        BorParameters borParams = _api.ChainSpec.Bor;
        IBorConfig borConfig = _api.Config<IBorConfig>();

        HeimdallHttpClient heimdallClient =
            new(borConfig.HeimdallUrl, _api.EthereumJsonSerializer, _api.LogManager);

        BorParamsHelper borHelper = new(borParams);

        ReadOnlyTxProcessingEnv readOnlyTxProcessingEnv = new(
            _api.DbProvider,
            _api.ReadOnlyTrieStore,
            _api.BlockTree,
            _api.SpecProvider,
            _api.LogManager
        );

        BorValidatorSetContract validatorSetContract = new(
            readOnlyTxProcessingEnv,
            _api.TransactionProcessor,
            _api.AbiEncoder,
            borParams.ValidatorContractAddress
        );

        BorValidatorSetManager validatorSetManager = new(
            _api.ChainSpec.ChainId,
            _api.BlockTree,
            heimdallClient,
            borHelper,
            validatorSetContract
        );

        BorStateReceiverContract stateReceiverContract = new(
            _api.TransactionProcessor,
            _api.AbiEncoder,
            borParams.StateReceiverContractAddress
        );

        BorStateSyncManager stateSyncManager = new(
            _api.LogManager,
            _api.BlockTree,
            borHelper,
            heimdallClient,
            stateReceiverContract
        );

        BlockProcessor.BlockValidationTransactionsExecutor blockTransactionsExecutor =
            new(_api.TransactionProcessor, _api.StateProvider);

        return new BorBlockProcessor(
            // Bor specific stuff
            validatorSetManager,
            stateSyncManager,

            // Base BlockProcessor stuff
            _api.SpecProvider,
            _api.BlockValidator,
            NoBlockRewards.Instance,
            blockTransactionsExecutor,
            _api.StateProvider,
            _api.StorageProvider,
            _api.ReceiptStorage,
            _api.WitnessCollector,
            _api.LogManager
        );
    }
}