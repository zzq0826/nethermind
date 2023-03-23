using Nethermind.Core;

namespace Nethermind.Consensus.Bor;

public interface IBorValidatorSetContract
{
    BorSpan GetCurrentSpan(BlockHeader header);
    BorSpan GetCurrentValidators(BlockHeader header);
    void CommitSpan(BlockHeader header, HeimdallSpan span);
}