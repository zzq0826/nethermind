using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Consensus.Bor;

public class BorBlockTransferLogsTracer : IBlockTracer
{
    public IReadOnlyList<LogEntry[]> Logs => _logs;
    private readonly List<LogEntry[]> _logs = new();
    private readonly IStateProvider _stateProvider;

    private Address? _beneficiary;
    private BorTxTransferLogTracer? _currentTracer;

    public BorBlockTransferLogsTracer(IStateProvider stateProvider)
    {
        _stateProvider = stateProvider;
    }

    public void StartNewBlockTrace(Block block)
    {
        ArgumentNullException.ThrowIfNull(block.Header.GasBeneficiary);

        _logs.Clear();
        _beneficiary = block.Header.GasBeneficiary;
    }

    public void EndBlockTrace() { }

    public ITxTracer StartNewTxTrace(Transaction? tx)
    {
        ArgumentNullException.ThrowIfNull(tx?.SenderAddress);

        return _currentTracer = new(_stateProvider, tx.SenderAddress, _beneficiary!);
    }

    public void EndTxTrace()
    {
        _logs.Add(_currentTracer!.TxLogs);
        _currentTracer = null;
    }

    public bool IsTracingRewards => false;

    public void ReportReward(Address author, string rewardType, UInt256 rewardValue)
    {
        throw new NotImplementedException();
    }
}

public class BorTxTransferLogTracer : ITxTracer
{
    private static readonly Address _logAddress = new("0x0000000000000000000000000000000000001010");
    private static readonly Keccak _feeEventSig = new("0x4dfe1bbbcf077ddc3e01291eea2d5c70c2b422b415d95645b9adcfd678cb1d63");
    private static readonly Keccak _transferEventSig = new("0xe6497e3ee548a3372136af2fcb0696db31fc6cf20260707645068bd3fe97f3c4");

    private int _logCount = 0;
    private bool _insideLog = false;
    private readonly List<(int At, LogEntry Entry)> _logs = new();

    private readonly IStateProvider _stateProvider;
    private readonly (Address Address, UInt256 InitialBalance) _sender;
    private readonly (Address Address, UInt256 InitialBalance) _beneficiary;

    private LogEntry[]? _txLogs = null;
    public LogEntry[] TxLogs => _txLogs ?? throw new InvalidOperationException("TxLogs accessed before the transaction was traced");

    public BorTxTransferLogTracer(IStateProvider stateProvider, Address sender, Address beneficiary)
    {
        _stateProvider = stateProvider;
        _sender = (sender, stateProvider.GetBalance(sender));
        _beneficiary = (beneficiary, stateProvider.GetBalance(beneficiary));
    }

    private static LogEntry BuildTransferLog(Keccak eventSig, Address from, Address to, UInt256 amount, UInt256 input1, UInt256 input2, UInt256 output1, UInt256 output2)
    {
        return new(
            _logAddress,
            Bytes.Concat(
                amount.PaddedBytes(32),
                input1.PaddedBytes(32),
                input2.PaddedBytes(32),
                output1.PaddedBytes(32),
                output2.PaddedBytes(32)
            ),
            new[] {
                eventSig,
                new(_logAddress.Bytes.PadLeft(32)),
                new(from.Bytes.PadLeft(32)),
                new(to.Bytes.PadLeft(32))
            }
        );
    }

    public bool IsTracingReceipt => true;

    public void MarkAsFailed(Address recipient, long gasSpent, byte[] output, string error, Keccak? stateRoot = null)
    {
        _txLogs = new[] { _logs[^1].Entry };
    }

    public void MarkAsSuccess(Address recipient, long gasSpent, byte[] output, LogEntry[] logs, Keccak? stateRoot = null)
    {
        List<LogEntry> extendedLogs = new(logs.Length + _logs.Count);
        int i = 0;
        foreach ((int at, LogEntry entry) in _logs)
        {
            for (; i < at; i++)
                extendedLogs.Add(logs[i]);
            extendedLogs.Add(entry);
        }
        _txLogs = extendedLogs.ToArray();
    }

    public bool IsTracingFees => true;

    public void ReportFees(UInt256 fees, UInt256 burntFees)
    {
        UInt256 input1 = _sender.InitialBalance;
        UInt256 input2 = _beneficiary.InitialBalance;
        UInt256 output1 = input1 - fees;
        UInt256 output2 = input2 + fees;

        LogEntry log = BuildTransferLog(
            _feeEventSig,
            _sender.Address,
            _beneficiary.Address,
            fees,
            input1, input2,
            output1, output2
        );

        _logs.Add((_logCount, log));
    }

    public bool IsTracingActions => true;

    public void ReportAction(long gas, UInt256 value, Address from, Address to, ReadOnlyMemory<byte> input, ExecutionType callType, bool isPrecompileCall = false)
    {
        if (value.IsZero) return;

        switch (callType)
        {
            case ExecutionType.Transaction:
            case ExecutionType.Create:
            case ExecutionType.Create2:
            case ExecutionType.Call:
            case ExecutionType.CallCode:
                break;
            case ExecutionType.StaticCall:
            case ExecutionType.DelegateCall:
                return;
            default:
                throw new NotImplementedException();
        }

        UInt256 output1 = _stateProvider.GetBalance(from);
        UInt256 input2 = _stateProvider.GetBalance(to);

        UInt256 input1 = output1 + value;
        UInt256 output2 = input2 + value;

        LogEntry log = from != to ? BuildTransferLog(_transferEventSig, from, to, value, input1, input2, output1, output2)
                                    : BuildTransferLog(_transferEventSig, from, to, value, input1, input1, output2, output2);

        _logs.Add((_logCount, log));
    }

    public void ReportActionError(EvmExceptionType evmExceptionType) { }

    public void ReportActionEnd(long gas, ReadOnlyMemory<byte> output) { }

    public void ReportActionEnd(long gas, Address deploymentAddress, ReadOnlyMemory<byte> deployedCode) { }


    public bool IsTracingInstructions => true;

    public void StartOperation(int depth, long gas, Instruction opcode, int pc, bool isPostMerge = false)
    {
        _insideLog = opcode switch
        {
            Instruction.LOG0 or Instruction.LOG1 or Instruction.LOG2 or Instruction.LOG3 or Instruction.LOG4 => true,
            _ => false,
        };
        if (_insideLog) _logCount++;
    }

    public void ReportOperationError(EvmExceptionType error)
    {
        if (!_insideLog)
            return;

        _logCount--;
        _insideLog = false;
    }

    public void ReportGasUpdateForVmTrace(long refund, long gasAvailable) { }

    public void ReportMemoryChange(long offset, in ReadOnlySpan<byte> data) { }

    public void ReportOperationRemainingGas(long gas) { }

    public void ReportStackPush(in ReadOnlySpan<byte> stackItem) { }

    public bool IsTracingOpLevelStorage => false;

    public bool IsTracingMemory => false;

    public bool IsTracingRefunds => false;

    public bool IsTracingCode => false;

    public bool IsTracingStack => false;

    public bool IsTracingBlockHash => false;

    public bool IsTracingAccess => false;

    public bool IsTracingState => false;

    public bool IsTracingStorage => false;

    public void LoadOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> value) { }

    public void ReportAccess(IReadOnlySet<Address> accessedAddresses, IReadOnlySet<StorageCell> accessedStorageCells) { }

    public void ReportAccountRead(Address address) { }

    public void ReportBalanceChange(Address address, UInt256? before, UInt256? after) { }

    public void ReportBlockHash(Keccak blockHash) { }

    public void ReportByteCode(byte[] byteCode) { }

    public void ReportCodeChange(Address address, byte[]? before, byte[]? after) { }

    public void ReportExtraGasPressure(long extraGasPressure) { }

    public void ReportNonceChange(Address address, UInt256? before, UInt256? after) { }

    public void ReportRefund(long refund) { }

    public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress) { }

    public void ReportStorageChange(in ReadOnlySpan<byte> key, in ReadOnlySpan<byte> value) { }

    public void ReportStorageChange(in StorageCell storageCell, byte[] before, byte[] after) { }

    public void ReportStorageRead(in StorageCell storageCell) { }

    public void SetOperationMemory(List<string> memoryTrace) { }

    public void SetOperationMemorySize(ulong newSize) { }

    public void SetOperationStack(List<string> stackTrace) { }

    public void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> newValue, ReadOnlySpan<byte> currentValue) { }
}