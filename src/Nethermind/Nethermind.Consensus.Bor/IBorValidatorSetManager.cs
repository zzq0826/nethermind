using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public interface IBorValidatorSetManager
{
    void ProcessBlock(BlockHeader header);
}