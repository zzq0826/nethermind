// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Db.Blooms;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.State.Repositories;
using Nethermind.Trie.Pruning;

namespace Nethermind.Facade.Multicall;

public class MultiCallReadOnlyBlocksProcessingEnv : ReadOnlyTxProcessingEnvBase, IDisposable
{
    //private readonly ITrieStore _trieStore;
    private readonly ILogManager? _logManager;
    private readonly IBlockValidator _blockValidator;
    public ISpecProvider SpecProvider { get; }
    public IWorldStateManager WorldStateManager { get; }

    public IVirtualMachine VirtualMachine { get; }
    public OverridableCodeInfoRepository CodeInfoRepository { get; }
    private readonly bool _doValidation = false;
    // We need ability to get many instances that do not conflict in terms of editable tmp storage - thus we implement env cloning
    public static MultiCallReadOnlyBlocksProcessingEnv Create(
        bool traceTransfers,
        IWorldStateManager worldStateManager,
        IReadOnlyBlockTree roBlockTree,
        IReadOnlyDbProvider readOnlyDbProvider,
        ISpecProvider? specProvider,
        ILogManager? logManager = null,
        bool doValidation = false)
    {
        IReadOnlyDbProvider dbProvider = new ReadOnlyDbProvider(readOnlyDbProvider, true);
        TrieStore trieStore = new(readOnlyDbProvider.StateDb, logManager);

        IBlockStore blockStore = new BlockStore(dbProvider.BlocksDb);
        IHeaderStore headerStore = new HeaderStore(dbProvider.HeadersDb, dbProvider.BlockNumbersDb);
        const int badBlocksStored = 1;
        IBlockStore badBlockStore = new BlockStore(dbProvider.BadBlocksDb, badBlocksStored);

        BlockTree tmpBlockTree = new(blockStore,
            headerStore,
            dbProvider.BlockInfosDb,
            dbProvider.MetadataDb,
            badBlockStore,
            new ChainLevelInfoRepository(readOnlyDbProvider.BlockInfosDb),
            specProvider,
            NullBloomStorage.Instance,
            new SyncConfig(),
            logManager);
        var blockTree = new NonDistructiveBlockTreeOverlay(roBlockTree, tmpBlockTree);

        return new MultiCallReadOnlyBlocksProcessingEnv(
            traceTransfers,
            worldStateManager,
            roBlockTree,
            dbProvider,
            //trieStore,
            blockTree,
            specProvider,
            logManager,
            doValidation);
    }

    public MultiCallReadOnlyBlocksProcessingEnv Clone(bool traceTransfers, bool doValidation) =>
        Create(traceTransfers, WorldStateManager, ReadOnlyBlockTree, DbProvider, SpecProvider, _logManager, doValidation);

    public IReadOnlyDbProvider DbProvider { get; }

    private MultiCallReadOnlyBlocksProcessingEnv(
        bool traceTransfers,
        IWorldStateManager worldStateManager,
        IReadOnlyBlockTree roBlockTree,
        IReadOnlyDbProvider readOnlyDbProvider,
        //ITrieStore trieStore,
        IBlockTree blockTree,
        ISpecProvider? specProvider,
        ILogManager? logManager = null,
        bool doValidation = false)
        : base(worldStateManager, blockTree, logManager)
    {
        ReadOnlyBlockTree = roBlockTree;
        DbProvider = readOnlyDbProvider;
        //_trieStore = trieStore;
        WorldStateManager = worldStateManager;
        _logManager = logManager;
        SpecProvider = specProvider;
        _doValidation = doValidation;
        CodeInfoRepository = new OverridableCodeInfoRepository(new CodeInfoRepository());

        VirtualMachine = new VirtualMachine(BlockhashProvider, specProvider, CodeInfoRepository, logManager);

        HeaderValidator headerValidator = new(
            BlockTree,
            Always.Valid,
            SpecProvider,
            _logManager);

        BlockValidator blockValidator = new(
            new TxValidator(SpecProvider!.ChainId),
            headerValidator,
            Always.Valid,
            SpecProvider,
            _logManager);

        _blockValidator = new MultiCallBlockValidatorProxy(blockValidator);
    }

    public IReadOnlyBlockTree ReadOnlyBlockTree { get; set; }

    public IBlockProcessor GetProcessor(Hash256 stateRoot)
    {
        //StateProvider.StateRoot = stateRoot;
        IReadOnlyTransactionProcessor transactionProcessor = Build(stateRoot);

        return new BlockProcessor(SpecProvider,
            _blockValidator,
            NoBlockRewards.Instance,
            new BlockProcessor.BlockValidationTransactionsExecutor(transactionProcessor, StateProvider),
            StateProvider,
            NullReceiptStorage.Instance,
            NullWitnessCollector.Instance,
            _logManager);
    }

    public IReadOnlyTransactionProcessor Build(Hash256 stateRoot) => new ReadOnlyTransactionProcessor(new TransactionProcessor(SpecProvider, StateProvider, VirtualMachine, CodeInfoRepository, _logManager, !_doValidation), StateProvider, stateRoot);

    public void Dispose()
    {
        //_trieStore.Dispose();
        DbProvider.Dispose();
    }
}
