using Nethermind.Facade.Proxy;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Newtonsoft.Json;

namespace Nethermind.Consensus.Bor;

public class HeimdallHttpClient : IHeimdallClient
{
    public static void RegisterConverters(IJsonSerializer serializer)
    {
        serializer.RegisterConverter(new BorValidatorJsonConverter());
        serializer.RegisterConverter(new HeimdallSpanJsonConverter());
        serializer.RegisterConverter(new StateSyncEventRecordJsonConverter());
    }

    private const int StateSyncEventsLimit = 50;
    private readonly string _heimdallAddr;
    private readonly IHttpClient _httpClient;

    public HeimdallHttpClient(string heimdallAddr, IJsonSerializer jsonSerializer, ILogManager logManager)
    {
        _heimdallAddr = heimdallAddr;
        _httpClient = new DefaultHttpClient(new HttpClient(), jsonSerializer, logManager);
    }

    // TODO: Make this async
    // Right now this is call in block processing context, which assumes syncronous calls
    public HeimdallSpan GetSpan(long number)
    {
        HeimdallResponse<HeimdallSpan> response =
            _httpClient.GetAsync<HeimdallResponse<HeimdallSpan>>($"{_heimdallAddr}/bor/span/{number}").Result;
        return response.Result ?? throw new Exception($"Heimdall returned null span for number {number}");
    }

    // TODO: Make this async
    // Right now this is call in block processing context, which assumes syncronous calls
    public StateSyncEventRecord[] StateSyncEvents(ulong fromId, ulong toTime)
    {
        string StateSyncEndpoint(ulong fromId, ulong toTime) =>
            $"{_heimdallAddr}/clerk/event-record/list?from-id={fromId}&to-time={toTime}&limit={StateSyncEventsLimit}";

        List<StateSyncEventRecord> eventRecords = new();

        while (true)
        {
            System.Console.WriteLine($"StateSyncEvents: fromId={fromId}, toTime={toTime}");
            HeimdallResponse<StateSyncEventRecord[]> response =
                _httpClient.GetAsync<HeimdallResponse<StateSyncEventRecord[]>>(StateSyncEndpoint(fromId, toTime)).Result;
            System.Console.WriteLine($"StateSyncEvents Result: {response.Result?.Length}");

            if (response.Result is null)
                break;

            eventRecords.AddRange(response.Result);

            if (response.Result.Length < StateSyncEventsLimit)
                break;

            fromId += StateSyncEventsLimit;
        }

        eventRecords.Sort((e1, e2) => e1.Id.CompareTo(e2.Id));

        System.Console.WriteLine("Out of StateSyncEvents");

        return eventRecords.ToArray();
    }

    private class HeimdallResponse<T>
    {
        public long Height { get; init; }
        public T? Result { get; set; }
    }
}
