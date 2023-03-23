using Nethermind.Config;

namespace Nethermind.Consensus.Bor;

public interface IBorConfig : IConfig
{
    [ConfigItem(Description = "The URL of the Heimdall node to connect to.", DefaultValue = "http://localhost:8545")]
    string HeimdallUrl { get; set; }
}