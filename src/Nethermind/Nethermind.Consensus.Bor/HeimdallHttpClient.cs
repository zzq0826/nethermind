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

    public HeimdallSpan GetSpan(long number)
    {
        // TODO: Make this async
        // Right now this is call in block processing context, which assumes syncronous calls
        HeimdallSpanResponse response =
            _httpClient.GetAsync<HeimdallSpanResponse>($"{_heimdallAddr}/bor/span/{number}").Result;
        return response.Result ?? throw new Exception($"Heimdall returned null span for number {number}");
    }

    private class HeimdallSpanResponse
    {
        public long Height { get; init; }
        public HeimdallSpan? Result { get; set; }
    }
}