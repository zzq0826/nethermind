namespace Nethermind.Tools.Evm.Commands;

public class TraceOptions
{
    public bool Memory { get; set; }
    public bool NoMemory { get; set; }
    public bool NoReturnData { get; set; }
    public bool NoStack { get; set; }
    public bool ReturnData { get; set; }
}

public class T8nOutput
{
    public bool Alloc { get; set; }
    public bool Result { get; set; }
    public bool Body { get; set; }
}

public class T8N
{
    public static async Task<int> HandleAsync(
        FileInfo? inputAlloc,
        FileInfo? inputEnv,
        FileInfo? inputTxs,
        FileInfo? outputAlloc,
        FileInfo? outputBaseDir,
        string? outputBody,
        string? outputResult,
        int stateChainId,
        string? stateFork,
        int stateReward,
        TraceOptions traceOpts
        )
    {
        var t8n = new T8N();
        await t8n.RunAsync(
            inputAlloc,
            inputEnv,
            inputTxs,
            outputAlloc,
            outputBaseDir,
            outputBody,
            outputResult,
            stateChainId,
            stateFork,
            stateReward,
            false,
            true,
            true,
            true,
            false
            );
        return 0;
    }
    
    public Task RunAsync(
        FileInfo? inputAlloc,
        FileInfo? inputEnv,
        FileInfo? inputTxs,
        FileInfo? outputAlloc,
        FileInfo? outputBaseDir,
        string? outputBody,
        string? outputResult,
        int stateChainId,
        string? stateFork,
        int stateReward,
        bool traceMemory,
        bool traceNoMemory,
        bool traceNoReturnData,
        bool traceNoStack,
        bool traceReturnData)
    {
        Console.WriteLine("[t8n] It works!");
        return Task.CompletedTask;
    }
}
