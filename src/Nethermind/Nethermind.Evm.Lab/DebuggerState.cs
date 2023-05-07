// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using DotNetty.Common.Utilities;
using DebuggerStateEvents;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Components;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Test;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Int256;
using Nethermind.Specs.Forks;
using Nethermind.Evm.Tracing.DebugTrace;
using Nethermind.Evm;

namespace DebuggerStateEvents
{
    public record MoveNext(bool onlyOneStep) : ActionsBase;
    public record BytecodeInserted(string bytecode) : ActionsBase;
    public record BytecodeInsertedB(byte[] bytecode) : ActionsBase;
    public record FileLoaded(string filePath) : ActionsBase;
    public record SetForkChoice(IReleaseSpec forkName) : ActionsBase;
    public record ThrowError(string error) : ActionsBase;
    public record SetGasMode(bool ignore, long gasValue) : ActionsBase;
    public record SetBreakpoint(int pc, Func<EvmState, bool> condition = null) : ActionsBase;
    public record SetGlobalCheck(Func<EvmState, bool> condition = null) : ActionsBase;
    public record Start : ActionsBase;
    public record Lock : ActionsBase;
    public record Update : ActionsBase;
    public record Reset : ActionsBase;
    public record Abort : ActionsBase;

}

namespace Nethermind.Evm.Lab
{
    public class DebuggerState : IState<DebuggerState>
    {
        public EthereumRestrictedInstance context = new(Cancun.Instance);
        public DebugTracer Tracer = new(new GethLikeTxTracer(GethTraceOptions.Default));
        public DebuggerState Initialize(bool resetBytecode = false)
        {
            if(resetBytecode)
            {
                SelectedFork = Cancun.Instance;
                byte[] bytecode = Core.Extensions.Bytes.FromHexString(Uri.IsWellFormedUriString(GlobalState.initialCmdArgument, UriKind.Absolute) ? File.OpenText(GlobalState.initialCmdArgument).ReadToEnd() : GlobalState.initialCmdArgument);
                RuntimeContext = CodeInfoFactory.CreateCodeInfo(bytecode, SelectedFork);
            }

            context = new(SelectedFork);
            Tracer = new(new GethLikeTxTracer(GethTraceOptions.Default));

            Tracer.BreakPointReached += () =>
            {
                EventsSink.EnqueueEvent(new Update());
            };

            Tracer.ExecutionThreadSet += () =>
            {
                EventsSink.EnqueueEvent(new Lock());
                
            };

            return this.Setup();
        }
        public DebuggerState()
        {
            AvailableGas = VirtualMachineTestsBase.DefaultBlockGasLimit;
            SelectedFork = Cancun.Instance;
        }

        public EventsSink EventsSink { get; } = new EventsSink();
        private Thread WorkThread { get; set; }
        public IReleaseSpec SelectedFork { get; set; }
        public ICodeInfo RuntimeContext { get; set; }
        public long AvailableGas { get; private set; }
        public bool IsActive => Tracer.CanReadState;

        public DebuggerState Setup()
        {
            WorkThread = new Thread(() => context.Execute(Tracer, AvailableGas, RuntimeContext.MachineCode));
            return this;
        }
        public DebuggerState Start()
        {
            WorkThread?.Start();
            return this;
        }
        public DebuggerState Next()
        {
            Tracer.MoveNext(executeOneStep: false);
            return this;
        }
        public DebuggerState Step()
        {
            Tracer.MoveNext(executeOneStep: true);
            return this;
        }
        public DebuggerState Abort()
        {
            Tracer.Abort();
            WorkThread?.Interrupt();
            return this;
        }
        public DebuggerState SetGas(long gas)
        {
            AvailableGas = gas;
            if(Tracer.CanReadState)
            {
                Tracer.CurrentState.GasAvailable = AvailableGas;
            }
            return this;
        }

        public DebuggerState SetFork(IReleaseSpec forkname)
        {
            SelectedFork = forkname;
            return this;
        }


        IState<DebuggerState> IState<DebuggerState>.Initialize(DebuggerState seed) => seed;

        public async Task<bool> MoveNext()
        {
            if (this.EventsSink.TryDequeueEvent(out var currentEvent))
            {
                lock (this)
                {
                    try
                    {
                        DebuggerState.Update(this, currentEvent).GetState();
                    }
                    catch (Exception ex)
                    {
                        var dialogView = MainView.ShowError(ex.Message,
                            () =>
                            {
                                this.EventsSink.EnqueueEvent(new Reset());
                            }
                        );
                    }
                }
                return true;
            }
            return false;
        }

        public static IState<DebuggerState> Update(IState<DebuggerState> state, ActionsBase msg)
        {
            switch (msg)
            {
                case MoveNext nxtMsg:
                    return nxtMsg.onlyOneStep ? state.GetState().Step() : state.GetState().Next();
                case FileLoaded flMsg:
                    {
                        var file = File.OpenText(flMsg.filePath);
                        if (file == null)
                        {
                            state.EventsSink.EnqueueEvent(new ThrowError($"File {flMsg.filePath} not found"), true);
                            break;
                        }

                        state.EventsSink.EnqueueEvent(new BytecodeInserted(file.ReadToEnd()), true);

                        break;
                    }
                case BytecodeInserted biMsg:
                    {
                        state.EventsSink.EnqueueEvent(new BytecodeInsertedB(Nethermind.Core.Extensions.Bytes.FromHexString(biMsg.bytecode)), true);
                        break;
                    }
                case BytecodeInsertedB biMsg:
                    {
                        state.GetState().RuntimeContext = CodeInfoFactory.CreateCodeInfo(biMsg.bytecode, state.GetState().SelectedFork);
                        return state;
                    }
                case SetForkChoice frkMsg:
                    {
                        state.GetState().context = new(frkMsg.forkName);
                        return state.GetState().SetFork(frkMsg.forkName);
                    }
                case SetGasMode gasMsg:
                    {
                        return state.GetState().SetGas(gasMsg.ignore ? int.MaxValue : gasMsg.gasValue);
                    }

                case SetBreakpoint brkMsg:
                    {
                        state.GetState().Tracer.SetBreakPoint(brkMsg.pc, brkMsg.condition);
                        break;
                    }
                case SetGlobalCheck chkMsg:
                    {
                        state.GetState().Tracer.SetCondtion(chkMsg.condition);
                        break;
                    }

                case Start _:
                    {
                        return state.GetState().Start();
                    }
                case Reset _:
                    {
                        return state.GetState().Abort().Initialize();
                    }
                case Update _:
                    {
                        return state.GetState();
                    }
                case Lock _:
                    {
                        return state.GetState();
                    }
                case Abort _:
                    {
                        return state.GetState().Abort().Setup();
                    }
                case ThrowError errMsg:
                    {
                        throw new Exception(errMsg.error);
                    }
            }
            return state;
        }
    }
}
