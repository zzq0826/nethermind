// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Verkle.Curve;

namespace Nethermind.Evm.TransactionProcessing;

public class StatelessTransactionProcessor: ITransactionProcessor
{
    private readonly ILogManager _logManager;
    private readonly ISpecProvider _specProvider;
    private readonly IVirtualMachine _virtualMachine;

    public StatelessTransactionProcessor(
        ISpecProvider? specProvider,
        IVirtualMachine? virtualMachine,
        ILogManager? logManager)
    {
        _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
        _specProvider = specProvider ?? throw new ArgumentNullException(nameof(specProvider));
        _virtualMachine = virtualMachine ?? throw new ArgumentNullException(nameof(virtualMachine));
    }

    public void Execute(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        VerkleWorldState worldState = new(block.ExecutionWitness!, Banderwagon.FromBytes(block.StateRoot!.Bytes)!.Value, _logManager);
        TransactionProcessor txn = new(_specProvider, worldState, _virtualMachine, _logManager);
        txn.Execute(transaction, block, txTracer);

    }

    public void CallAndRestore(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        VerkleWorldState worldState = new (block.ExecutionWitness!, Banderwagon.FromBytes(block.StateRoot!.Bytes)!.Value, _logManager);
        TransactionProcessor txn = new (_specProvider, worldState, _virtualMachine, _logManager);
        txn.CallAndRestore(transaction, block, txTracer);
    }

    public void BuildUp(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        VerkleWorldState worldState = new (block.ExecutionWitness!, Banderwagon.FromBytes(block.StateRoot!.Bytes)!.Value, _logManager);
        TransactionProcessor txn = new (_specProvider, worldState, _virtualMachine, _logManager);
        txn.BuildUp(transaction, block, txTracer);
    }

    public void Trace(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        VerkleWorldState worldState = new (block.ExecutionWitness!, Banderwagon.FromBytes(block.StateRoot!.Bytes)!.Value, _logManager);
        TransactionProcessor txn = new (_specProvider, worldState, _virtualMachine, _logManager);
        txn.Trace(transaction, block, txTracer);
    }

    public ITransactionProcessor WithNewStateProvider(IWorldState worldState)
    {
        return this;
    }
}
