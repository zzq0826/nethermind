using Nethermind.Abi;
using Nethermind.Blockchain.Contracts;
using Nethermind.Core;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Consensus.Bor;

public class BorStateReceiverContract : CallableContract, IBorStateReceiverContract
{
    public BorStateReceiverContract(
        ITransactionProcessor transactionProcessor,
        IAbiEncoder abiEncoder,
        Address contractAddress
    ) : base(transactionProcessor, abiEncoder, contractAddress)
    {
    }

    public void CommitState(BlockHeader header, StateSyncEventRecord eventRecord)
    {
        byte[] eventRecordBytes = Rlp.Encode(
            Rlp.Encode(eventRecord.Id),
            Rlp.Encode(eventRecord.ContractAddress),
            Rlp.Encode(eventRecord.Data),
            Rlp.Encode(eventRecord.TxHash),
            Rlp.Encode(eventRecord.LogIndex),
            Rlp.Encode(eventRecord.ChainId)
        ).Bytes;

        Call(header, "commitState", Address.SystemUser, UnlimitedGas, eventRecord.Time.ToUnixTimeSeconds(), eventRecordBytes);
    }

    public ulong LastStateId(BlockHeader header)
    {
        return (ulong)(UInt256)CallAndRestore(header, "lastStateId", Address.SystemUser)[0];
    }
}