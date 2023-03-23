using Nethermind.Facade.Proxy;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Consensus.Bor;

public class HeimdallHttpClient : IHeimdallClient
{
    protected readonly string _heimdallAddr;
    protected readonly IHttpClient _httpClient;

    public HeimdallHttpClient(string heimdallAddr, IJsonSerializer jsonSerializer, ILogManager logManager)
    {
        _heimdallAddr = heimdallAddr;
        _httpClient = new DefaultHttpClient(new HttpClient(), jsonSerializer, logManager);
    }

    public HeimdallSpan? GetSpan(long number)
    {
        throw new NotImplementedException();
    }
}