using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.Test;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NUnit.Framework;

namespace Nethermind.Consensus.Bor.Test;

public class TransferLogsTracerTests
{
    private static readonly Address _logAddress = new("0x0000000000000000000000000000000000001010");
    private static readonly Keccak _feeEventSig = new("0x4dfe1bbbcf077ddc3e01291eea2d5c70c2b422b415d95645b9adcfd678cb1d63");
    private static readonly Keccak _transferEventSig = new("0xe6497e3ee548a3372136af2fcb0696db31fc6cf20260707645068bd3fe97f3c4");

    private ISpecProvider _specProvider;
    private IStateProvider _stateProvider;
    private ITransactionProcessor _transactionProcessor;
    private IEthereumEcdsa _ethereumEcdsa;

    [SetUp]
    public void Setup()
    {
        ReleaseSpec spec = new()
        {
            IsEip158Enabled = true,
            IsEip158IgnoringSystem = false
        };
        _specProvider = new TestSpecProvider(spec);

        TrieStore trieStore = new(new MemDb(), LimboLogs.Instance);

        _stateProvider = new StateProvider(trieStore, new MemDb(), LimboLogs.Instance);
        _stateProvider.CreateAccount(TestItem.AddressA, 1.Ether());
        _stateProvider.Commit(spec);
        _stateProvider.CommitTree(0);

        StorageProvider storageProvider = new(trieStore, _stateProvider, LimboLogs.Instance);
        VirtualMachine virtualMachine = new(TestBlockhashProvider.Instance, _specProvider, LimboLogs.Instance);
        _transactionProcessor = new TransactionProcessor(_specProvider, _stateProvider, storageProvider, virtualMachine, LimboLogs.Instance);
        _ethereumEcdsa = new EthereumEcdsa(_specProvider.ChainId, LimboLogs.Instance);
    }

    [Test]
    public void SimpleTransfer()
    {
        UInt256 amount = 1.GWei();

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressB)
            .WithValue(amount)
            .WithMaxFeePerGas(1.GWei())
            .SignedAndResolved(_ethereumEcdsa, TestItem.PrivateKeyA, true)
            .TestObject;

        UInt256 initialBalanceFrom = _stateProvider.GetBalance(tx.SenderAddress!);
        UInt256 initialBalanceTo = _stateProvider.GetBalance(tx.To!);


        Block block = Build.A.Block
            .WithNumber(1)
            .WithTransactions(tx)
            .WithAuthor(TestItem.AddressC)
            .TestObject;

        UInt256 initialBalanceBeneficiary = _stateProvider.GetBalance(block.Header.GasBeneficiary!);

        CallOutputTracer callOutputTracer = new();
        BorTxTransferLogTracer transferLogsTracer = new(_stateProvider, tx.SenderAddress!, block.Header.GasBeneficiary!);
        CompositeTxTracer tracer = new(transferLogsTracer, callOutputTracer);

        _transactionProcessor.Execute(tx, block.Header, tracer);

        UInt256 fee = tx.CalculateEffectiveGasPrice(_specProvider.GetSpec(block.Header).IsEip1559Enabled, block.Header.BaseFeePerGas) * (UInt256)callOutputTracer.GasSpent;

        AssertTransferLogsAreEqual(
            transferLogsTracer.TxLogs[0],
            _transferEventSig,
            tx.SenderAddress!,
            tx.To!,
            amount,
            initialBalanceFrom - fee,
            initialBalanceTo,
            initialBalanceFrom - fee - amount,
            initialBalanceTo + amount
        );
        AssertTransferLogsAreEqual(
            transferLogsTracer.TxLogs[1],
            _feeEventSig,
            tx.SenderAddress!,
            block.Header.GasBeneficiary!,
            fee,
            initialBalanceFrom,
            initialBalanceBeneficiary,
            initialBalanceFrom - fee,
            initialBalanceBeneficiary + fee
        );
    }

    [Test]
    public void SimpleTransferSenderEqualsRecipient()
    {
        UInt256 amount = 1.GWei();
        UInt256 initialBalanceSender = _stateProvider.GetBalance(TestItem.AddressA);
        UInt256 initialBalanceBeneficiary = _stateProvider.GetBalance(TestItem.AddressB);

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressA)
            .WithValue(amount)
            .WithMaxFeePerGas(1.GWei())
            .SignedAndResolved(_ethereumEcdsa, TestItem.PrivateKeyA, true)
            .TestObject;


        Block block = Build.A.Block
            .WithNumber(1)
            .WithTransactions(tx)
            .WithAuthor(TestItem.AddressB)
            .TestObject;

        CallOutputTracer callOutputTracer = new();
        BorTxTransferLogTracer transferLogsTracer = new(_stateProvider, tx.SenderAddress!, block.Header.GasBeneficiary!);
        CompositeTxTracer tracer = new(transferLogsTracer, callOutputTracer);

        _transactionProcessor.Execute(tx, block.Header, tracer);

        UInt256 fee = tx.CalculateEffectiveGasPrice(_specProvider.GetSpec(block.Header).IsEip1559Enabled, block.Header.BaseFeePerGas) * (UInt256)callOutputTracer.GasSpent;

        AssertTransferLogsAreEqual(
            transferLogsTracer.TxLogs[0],
            _transferEventSig,
            tx.SenderAddress!,
            tx.To!,
            amount,
            initialBalanceSender - fee,
            initialBalanceSender - fee,
            initialBalanceSender - fee,
            initialBalanceSender - fee
        );
        AssertTransferLogsAreEqual(
            transferLogsTracer.TxLogs[1],
            _feeEventSig,
            tx.SenderAddress!,
            block.Header.GasBeneficiary!,
            fee,
            initialBalanceSender,
            initialBalanceBeneficiary,
            initialBalanceSender - fee,
            initialBalanceBeneficiary + fee
        );
    }

    private static void AssertTransferLogsAreEqual(LogEntry log, Keccak eventSig, Address from, Address to, UInt256 amount, UInt256 input1, UInt256 input2, UInt256 output1, UInt256 output2)
    {
        Assert.AreEqual(_logAddress, log.LoggersAddress, "logger address is not correct");

        Assert.AreEqual(32 * 5, log.Data.Length, "data length is not 5*32 bytes");

        UInt256[] data = log.Data.Chunk(32).Select(c => new UInt256(c, isBigEndian: true)).ToArray();

        Assert.AreEqual(5, data.Length, "data length is not 5");

        Assert.AreEqual(amount, data[0], "amount is not correct");
        Assert.AreEqual(input1, data[1], "input1 is not correct");
        Assert.AreEqual(input2, data[2], "input2 is not correct");
        Assert.AreEqual(output1, data[3], "output1 is not correct");
        Assert.AreEqual(output2, data[4], "output2 is not correct");

        Assert.AreEqual(4, log.Topics.Length);
        Assert.AreEqual(eventSig.Bytes.ToHexString(), log.Topics[0].Bytes.ToHexString(), "event signature is not correct");
        Assert.AreEqual(_logAddress.Bytes.PadLeft(32).ToHexString(), log.Topics[1].Bytes.ToHexString(), "logger address is not correct");
        Assert.AreEqual(from.Bytes.PadLeft(32).ToHexString(), log.Topics[2].Bytes.ToHexString(), "from address is not correct");
        Assert.AreEqual(to.Bytes.PadLeft(32).ToHexString(), log.Topics[3].Bytes.ToHexString(), "to address is not correct");
    }
}