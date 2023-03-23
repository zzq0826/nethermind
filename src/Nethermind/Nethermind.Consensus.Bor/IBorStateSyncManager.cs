using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public interface IBorStateSyncManager
{
    void ProcessBlock(BlockHeader header);
}