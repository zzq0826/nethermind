using Nethermind.Config;

namespace Nethermind.Consensus.Bor;

public interface IBorConfig : IConfig
{
    [ConfigItem(Description = "The URL of the Heimdall node to connect to.", DefaultValue = "http://127.0.0.1:1317")]
    string HeimdallUrl { get; set; }
}