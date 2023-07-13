using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.Threading.Tasks;
using Nethermind.Tools.Evm.Commands;

namespace Nethermind.Tools.Evm
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var rootCmd = new RootCommand();
            rootCmd.Name = "evm";
            ConfigureT8NCommand(ref rootCmd);

            await rootCmd.InvokeAsync(args);
        }
        
        static void ConfigureT8NCommand(ref RootCommand rootCmd)
        {
             var inputAllocOpt = new Option<FileInfo>("--Input.Alloc", description: "Input allocations", getDefaultValue: () => new FileInfo("alloc.json"));
             var inputEnvOpt = new Option<FileInfo>("--Input.Env", description: "Input environment", getDefaultValue: () => new FileInfo("env.json"));
             var inputTxsOpt = new Option<FileInfo>("--Input.Txs", description: "Input transactions", getDefaultValue: () => new FileInfo("txs.json"));
             var outputAllocOpt = new Option<FileInfo>("--Output.Alloc", description: "Output allocations", getDefaultValue: () => new FileInfo("alloc.json"));
             var outputBaseDirOpt = new Option<FileInfo>("--Output.BaseDir", description: "Output base directory");
             var outputBodyOpt = new Option<string>("--Output.Body", description: "Output body");
             var outputResultOpt = new Option<string>("--Output.Result", description: "Output result", getDefaultValue: () => "result.json");
             var stateChainIdOpt = new Option<int>("--State.ChainId", description: "State chain id", getDefaultValue: () => 1);
             var stateForkOpt = new Option<string>("--State.Fork", description: "State fork", getDefaultValue: () => "GrayGlacier");
             var stateRewardOpt = new Option<int>("--State.Reward", description: "State reward", getDefaultValue: () => 0);
             var traceMemoryOpt = new Option<bool>("--Trace.Memory", description: "Trace memory", getDefaultValue: () => false);
             var traceNoMemoryOpt = new Option<bool>("--Trace.NoMemory", description: "Trace no memory", getDefaultValue: () => true);
             var traceNoReturnDataOpt = new Option<bool>("--Trace.NoReturnData", description: "Trace no return data", getDefaultValue: () => true);
             var traceNoStackOpt = new Option<bool>("--Trace.NoStack", description: "Trace no stack", getDefaultValue: () => false);
             var traceReturnDataOpt = new Option<bool>("--Trace.ReturnData", description: "Trace return data", getDefaultValue: () => false);
                     
            var cmd = new Command("t8n", "EVM State Transition command")
            {
                inputAllocOpt,
                inputEnvOpt,
                inputTxsOpt,
                outputAllocOpt,
                outputBaseDirOpt,
                outputBodyOpt,
                outputResultOpt,
                stateChainIdOpt,
                stateForkOpt,
                stateRewardOpt,
                traceMemoryOpt,
                traceNoMemoryOpt,
                traceNoReturnDataOpt,
                traceNoStackOpt,
                traceReturnDataOpt,
            };
            
            cmd.AddAlias("transition");
            rootCmd.Add(cmd);
            
            

            cmd.SetHandler(
                async (context) =>
                {
                    // Note: https://learn.microsoft.com/en-us/dotnet/standard/commandline/model-binding#parameter-binding-more-than-16-options-and-arguments
                    // t8n accepts less options (15) than 16 but command extension methods supports max 8 anyway
                    var traceOpts = new Commands.TraceOptions()
                    {
                        Memory = context.ParseResult.GetValueForOption<bool>(traceMemoryOpt),
                        NoMemory = context.ParseResult.GetValueForOption<bool>(traceNoMemoryOpt),
                        NoReturnData = context.ParseResult.GetValueForOption<bool>(traceNoReturnDataOpt),
                        NoStack = context.ParseResult.GetValueForOption<bool>(traceNoStackOpt),
                        ReturnData = context.ParseResult.GetValueForOption<bool>(traceReturnDataOpt),
                    };
                    await T8N.HandleAsync(
                        context.ParseResult.GetValueForOption<FileInfo>(inputAllocOpt),
                        context.ParseResult.GetValueForOption<FileInfo>(inputEnvOpt),
                        context.ParseResult.GetValueForOption<FileInfo>(inputTxsOpt),
                        context.ParseResult.GetValueForOption<FileInfo>(outputAllocOpt),
                        context.ParseResult.GetValueForOption<FileInfo>(outputBaseDirOpt),
                        context.ParseResult.GetValueForOption<string>(outputBodyOpt),
                        context.ParseResult.GetValueForOption<string>(outputResultOpt),
                        context.ParseResult.GetValueForOption<int>(stateChainIdOpt),
                        context.ParseResult.GetValueForOption<string>(stateForkOpt),
                        context.ParseResult.GetValueForOption<int>(stateRewardOpt),
                        traceOpts
                        );
                });
        }
    }
    
    
}