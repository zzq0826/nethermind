using Nethermind.Abi;
using Nethermind.Blockchain.Contracts;
using Nethermind.Core;
using Nethermind.Evm.TransactionProcessing;

namespace Nethermind.Consensus.Bor;

public class BorValidatorSetContract : CallableContract, IBorValidatorSetContract
{
    public BorValidatorSetContract(ITransactionProcessor transactionProcessor, IAbiEncoder abiEncoder, Address contractAddress)
        : base(transactionProcessor, abiEncoder, contractAddress)
    {
    }

    public void CommitSpan(BlockHeader header, HeimdallSpan span)
    {
        throw new NotImplementedException();
    }

    public BorSpan GetCurrentSpan(BlockHeader header)
    {
        throw new NotImplementedException();
    }

    public BorSpan GetCurrentValidators(BlockHeader header)
    {
        throw new NotImplementedException();
    }
}