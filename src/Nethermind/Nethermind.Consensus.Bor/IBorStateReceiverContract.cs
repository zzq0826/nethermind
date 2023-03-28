using Nethermind.Blockchain.Contracts;
using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public interface IBorStateReceiverContract
{
    ulong LastStateId(BlockHeader header);
    void CommitState(BlockHeader header, StateSyncEventRecord eventRecord);
}