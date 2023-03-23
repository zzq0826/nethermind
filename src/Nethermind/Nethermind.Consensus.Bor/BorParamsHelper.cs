using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Consensus.Bor;

public class BorParamsHelper : IBorParamsHelper
{
    private readonly BorParameters _params;

    public BorParamsHelper(BorParameters borParameters)
    {
        _params = borParameters;
    }

    public bool IsSprintStart(long blockNumber)
    {
        throw new NotImplementedException();
    }
}