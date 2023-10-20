// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.Trie.Pruning;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Nethermind.Consensus.Processing
{
    public class ReadOnlyTxProcessingEnv : ReadOnlyTxProcessingEnvBase, IReadOnlyTxProcessorSource
    {
        private readonly OverridableCodeInfoRepository _codeInfoRepository;
        public ITransactionProcessor TransactionProcessor { get; set; }

        public ReadOnlyTxProcessingEnv(
            IDbProvider dbProvider,
            IReadOnlyTrieStore? trieStore,
            IBlockTree blockTree,
            ISpecProvider? specProvider,
            ILogManager? logManager)
            : this(dbProvider.AsReadOnly(false), trieStore, blockTree.AsReadOnly(), specProvider, logManager)
        {
        }

        public ReadOnlyTxProcessingEnv(
            IReadOnlyDbProvider readOnlyDbProvider,
            IReadOnlyTrieStore? trieStore,
            IReadOnlyBlockTree blockTree,
            ISpecProvider? specProvider,
            ILogManager? logManager
            ) : base(readOnlyDbProvider, trieStore, blockTree, logManager)
        {
            _codeInfoRepository = new OverridableCodeInfoRepository();
            IVirtualMachine machine = new VirtualMachine(BlockhashProvider, specProvider, _codeInfoRepository, logManager);
            TransactionProcessor = new TransactionProcessor(specProvider, StateProvider, machine, _codeInfoRepository, logManager);
        }

        public IReadOnlyTransactionProcessor Build(Keccak stateRoot, Dictionary<Address, AccountOverride>? accountOverrides = null)
        {
            StateProvider.ApplyStateOverrides();
            return new ReadOnlyTransactionProcessor(TransactionProcessor, StateProvider, _codeInfoRepository, stateRoot);
        }

        public IReadOnlyTransactionProcessor Build(Keccak stateRoot)
        {
            return new ReadOnlyTransactionProcessor(TransactionProcessor, StateProvider, _codeInfoRepository, stateRoot);
        }
    }
}
