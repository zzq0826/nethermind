using Nethermind.Abi;
using Nethermind.Blockchain.Contracts;
using Nethermind.Core;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Consensus.Bor;

public class BorStateReceiverContract : CallableContract, IBorStateReceiverContract
{
    public BorStateReceiverContract(
        ITransactionProcessor
        transactionProcessor,
        IAbiEncoder abiEncoder,
        Address contractAddress
    ) : base(transactionProcessor, abiEncoder, contractAddress)
    {
    }
}