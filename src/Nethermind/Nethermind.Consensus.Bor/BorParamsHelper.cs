using Nethermind.Core.Collections;
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
        => blockNumber % CalculateSprintSize(blockNumber) == 0;

    public long CalculateSprintSize(long blockNumber)
        => _params.Sprint.ToList().TryGetSearchedItem(
            blockNumber,
            (blockNumber, sprint) => blockNumber.CompareTo(sprint.Key),
            out KeyValuePair<long, long> sprint
        ) ? sprint.Value : throw new InvalidOperationException($"Sprint not found for block {blockNumber}");
}