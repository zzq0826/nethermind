using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Consensus.Bor;

public class BorSystemTransactionProcessor : ITransactionProcessor
{
    private readonly ILogger _logger;
    private readonly ISpecProvider _specProvider;
    private readonly IStateProvider _stateProvider;
    private readonly IStorageProvider _storageProvider;
    private readonly IWorldState _worldState;
    private readonly IVirtualMachine _virtualMachine;

    public BorSystemTransactionProcessor(ISpecProvider specProvider, IBlockTree blockTree, IStateProvider stateProvider, IStorageProvider storageProvider, ILogManager logManager)
    {
        _specProvider = specProvider;
        _stateProvider = stateProvider;
        _storageProvider = storageProvider;
        _worldState = new WorldState(stateProvider, storageProvider);
        BlockhashProvider blockhashProvider = new(blockTree, logManager);
        _virtualMachine = new VirtualMachine(blockhashProvider, specProvider, logManager);
        _logger = logManager.GetClassLogger();
    }

    public void CallAndRestore(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        Execute(transaction, block, txTracer, commit: true, restore: true);
    }

    public void BuildUp(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        _worldState.TakeSnapshot(true);
        Execute(transaction, block, txTracer, commit: false, restore: false);
    }

    public void Execute(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        Execute(transaction, block, txTracer, commit: true, restore: false);
    }

    public void Trace(Transaction transaction, BlockHeader block, ITxTracer txTracer)
    {
        Execute(transaction, block, txTracer, commit: false, restore: false);
    }

    private void Execute(Transaction transaction, BlockHeader block, ITxTracer txTracer, bool commit, bool restore)
    {
        IReleaseSpec spec = _specProvider.GetSpec(block);

        if (_logger.IsTrace) _logger.Trace($"Executing system tx {transaction.Hash}");

        Snapshot snapshot = _worldState.TakeSnapshot();
        byte statusCode = StatusCode.Failure;
        TransactionSubstate? substate = null;

        long spentGas = 0;
        Address recipient = transaction.To!;
        try
        {
            ExecutionEnvironment env = new()
            {
                TxExecutionContext = new TxExecutionContext(block, transaction.SenderAddress!, UInt256.Zero, transaction.BlobVersionedHashes!),
                Value = UInt256.Zero,
                TransferValue = UInt256.Zero,
                Caller = transaction.SenderAddress!,
                CodeSource = recipient,
                ExecutingAccount = recipient,
                InputData = transaction.Data,
                CodeInfo = _virtualMachine.GetCachedCodeInfo(_worldState, recipient, spec),
            };

            using (EvmState state = new(transaction.GasLimit, env, ExecutionType.Transaction, true, snapshot, false))
            {
                substate = _virtualMachine.Run(state, _worldState, txTracer);
                spentGas = transaction.GasLimit - state.GasAvailable;

                if (txTracer.IsTracingAccess)
                {
                    txTracer.ReportAccess(state.AccessedAddresses, state.AccessedStorageCells);
                }
            }

            if (substate.ShouldRevert || substate.IsError)
            {
                if (_logger.IsTrace) _logger.Trace("Restoring state from before transaction");
                _worldState.Restore(snapshot);
            }
            else
            {
                foreach (Address toBeDestroyed in substate.DestroyList)
                {
                    if (_logger.IsTrace) _logger.Trace($"Destroying account {toBeDestroyed}");

                    _storageProvider.ClearStorage(toBeDestroyed);
                    _stateProvider.DeleteAccount(toBeDestroyed);

                    if (txTracer.IsTracingRefunds) txTracer.ReportRefund(RefundOf.Destroy(spec.IsEip3529Enabled));
                }

                statusCode = StatusCode.Success;
            }
        }
        // TODO: OverflowException? still needed? hope not
        catch (Exception ex) when (ex is EvmException || ex is OverflowException)
        {
            if (_logger.IsTrace) _logger.Trace($"EVM EXCEPTION: {ex.GetType().Name}");
            _worldState.Restore(snapshot);
        }

        if (_logger.IsTrace) _logger.Trace($"Gas spent: {spentGas}");

        if (restore)
        {
            _storageProvider.Reset();
            _stateProvider.Reset();
            _stateProvider.Commit(spec);
        }
        else if (commit || !spec.IsEip658Enabled)
        {
            _storageProvider.Commit(txTracer.IsTracingState ? txTracer : NullStorageTracer.Instance);
            _stateProvider.Commit(spec, txTracer.IsTracingState ? txTracer : NullStateTracer.Instance);
        }

        if (txTracer.IsTracingReceipt)
        {
            Keccak? stateRoot = null;
            if (!spec.IsEip658Enabled)
            {
                _stateProvider.RecalculateStateRoot();
                stateRoot = _stateProvider.StateRoot;
            }

            if (statusCode == StatusCode.Failure)
            {
                txTracer.MarkAsFailed(
                    recipient, spentGas,
                    (substate?.ShouldRevert ?? false) ? substate.Output.ToArray() : Array.Empty<byte>(),
                    substate?.Error, stateRoot);
            }
            else
            {
                txTracer.MarkAsSuccess(
                    recipient, spentGas, substate!.Output.ToArray(),
                    substate!.Logs.Any() ? substate.Logs.ToArray() : Array.Empty<LogEntry>(),
                    stateRoot);
            }
        }
    }
}