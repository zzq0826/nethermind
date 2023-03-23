using System.Collections.Generic;
using Nethermind.Core;

namespace Nethermind.Specs.ChainSpecStyle;

public class BorParameters
{
    public Address ValidatorContractAddress { get; init; }

    public Address StateReceiverContractAddress { get; init; }

    public IDictionary<long, long> Period { get; init; }

    public IDictionary<long, long> Sprint { get; init; }

    public IDictionary<long, long> BackupMultiplier { get; init; }

    public long? JaipurBlockNumber { get; init; }

    public long? DelhiBlockNumber { get; init; }
}