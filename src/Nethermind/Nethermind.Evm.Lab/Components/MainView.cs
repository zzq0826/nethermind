// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using MachineState.Actions;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Componants;
using Nethermind.Evm.Lab.Components.GlobalViews;
using Nethermind.Evm.Lab.Components.MachineLab;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Specs.Forks;
using Terminal.Gui;
namespace Nethermind.Evm.Lab.Components;
internal class MainView : IComponent<MachineState>
{
    private string initialCmdArgument;

    private EthereumRestrictedInstance context = new(Cancun.Instance);
    private GethLikeTxTracer _tracer => new(GethTraceOptions.Default);
    private PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
    private Window MainPanel = new Window("EvmLaboratory");
    public MachineState InitialState;

    public MachineState? defaultValue;
    public bool isCached = false;
    public IComponent<MachineState>[] _components;
    public MainView(string pathOrBytecode)
    {
        initialCmdArgument = pathOrBytecode;
        InitialState = Initialize(new MachineState());

        _components = new IComponent<MachineState>[]{
            new HeaderView(),
            new MachineOverview(),
            new StackView(),
            new MemoryView(),
            new InputsView(),
            new ReturnView(),
            new StorageView(),
            new ProgramView(),
            new ConfigsView()
        };
    }

    public MachineState Initialize(MachineState state)
    {
        byte[] bytecode = Core.Extensions.Bytes.FromHexString(Uri.IsWellFormedUriString(initialCmdArgument, UriKind.Absolute) ? File.OpenText(initialCmdArgument).ReadToEnd() : initialCmdArgument);

        state.RuntimeContext = CodeInfoFactory.CreateCodeInfo(bytecode, InitialState?.SelectedFork ?? Cancun.Instance);
        state.CallData = Array.Empty<byte>();
        var resultTraces = context.Execute(_tracer, long.MaxValue, bytecode).BuildResult();
        EventsSink.EnqueueEvent(new UpdateState(resultTraces), true);
        return state;
    }
    public static Dialog ShowError(string mesg, Action? cancelHandler = null)
    {
        var cancel = new Button("OK")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(5)
        };

        if(cancelHandler is not null)
            cancel.Clicked += cancelHandler;

        var dialog = new Dialog("Error", 60, 7, cancel)
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Dim.Percent(25),
            Height = Dim.Percent(25),
            ColorScheme = Colors.TopLevel,
        };
        cancel.Clicked += () => dialog.RequestStop();

        var entry = new TextView()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Percent(50),
            Enabled = false,
            Text = mesg,
            WordWrap = true,
        };
        dialog.Add(entry);
        Application.Run(dialog);
        return dialog;
    }
    public void Run(MachineState _)
    {
        bool firstRender = true;
        

        HookKeyboardEvents();
        Application.Init();
        Application.MainLoop.Invoke(
            async () =>
            {
                do
                {
                    if (EventsSink.TryDequeueEvent(out var currentEvent))
                    {
                        lock (InitialState)
                        {
                            try
                            {
                                InitialState = Update(InitialState, currentEvent).GetState();
                                if (firstRender)
                                {
                                    Application.Top.Add(_components[0].View(InitialState).Item1, View(InitialState).Item1);
                                    firstRender = false;
                                }
                                else View(InitialState);
                            }
                            catch (Exception ex)
                            {
                                var dialogView = ShowError(ex.Message,
                                    () =>
                                    {
                                        EventsSink.EnqueueEvent(new Reset());
                                    }
                                );
                            }
                        }
                    }
                }
                while (firstRender || await timer.WaitForNextTickAsync());
            });

        Application.Run();
        Application.Shutdown();
    }
    public (View, Rectangle?) View(IState<MachineState> state, Rectangle? rect = null)
    {
        IComponent<MachineState> _component_cpu = _components[1];
        IComponent<MachineState> _component_stk = _components[2];
        IComponent<MachineState> _component_ram = _components[3];
        IComponent<MachineState> _component_inpt = _components[4];
        IComponent<MachineState> _component_rtrn = _components[5];
        IComponent<MachineState> _component_strg = _components[6];
        IComponent<MachineState> _component_pgr = _components[7];
        IComponent<MachineState> _component_cnfg = _components[8];

        var (view1, rect1) = _component_cpu.View(state, new Rectangle(0, 0, Dim.Percent(30), 10));
        var (view2, rect2) = _component_stk.View(state, rect1.Value with
        {
            Y = Pos.Bottom(view1),
            Height = Dim.Percent(45)
        });
        var (view3, rect3) = _component_ram.View(state, rect2.Value with
        {
            Y = Pos.Bottom(view2),
            Width = Dim.Fill()
        });
        var (view4, rect4) = _component_inpt.View(state, rect1.Value with
        {
            X = Pos.Right(view1),
            Width = Dim.Percent(50)
        });
        var (view5, rect5) = _component_strg.View(state, rect4.Value with
        {
            Y = Pos.Bottom(view4),
            Width = Dim.Percent(50),
            Height = Dim.Percent(25),
        });
        var (view6, rect6) = _component_rtrn.View(state, rect4.Value with
        {
            Y = Pos.Bottom(view5),
            Height = Dim.Percent(20),
            Width = Dim.Percent(50)
        });
        var (view8, rect8) = _component_cnfg.View(state, rect4.Value with
        {
            X = Pos.Right(view4),
            Width = Dim.Percent(20)
        });
        var (view7, rect7) = _component_pgr.View(state, rect8.Value with
        {
            Y = Pos.Bottom(view8),
            Height = Dim.Percent(45),
            Width = Dim.Percent(20)
        });

        if (!isCached)
            MainPanel.Add(view1, view4, view2, view3, view5, view6, view7, view8);
        isCached = true;
        return (MainPanel, null);
    }

    public IState<MachineState> Update(IState<MachineState> state, ActionsBase msg)
    {
        switch (msg)
        {
            case Goto idxMsg:
                return state.GetState().Goto(idxMsg.index);
            case MoveNext _:
                return state.GetState().Next();
            case MoveBack _:
                return state.GetState().Previous();
            case FileLoaded flMsg:
                {
                    var file = File.OpenText(flMsg.filePath);
                    if (file == null)
                    {
                        EventsSink.EnqueueEvent(new ThrowError($"File {flMsg.filePath} not found"), true);
                        break;
                    }

                    EventsSink.EnqueueEvent(new BytecodeInserted(file.ReadToEnd()), true);

                    break;
                }
            case BytecodeInserted biMsg:
                {
                    EventsSink.EnqueueEvent(new BytecodeInsertedB(Nethermind.Core.Extensions.Bytes.FromHexString(biMsg.bytecode)), true);
                    break;
                }
            case BytecodeInsertedB biMsg:
                {
                    state.GetState().RuntimeContext = CodeInfoFactory.CreateCodeInfo(biMsg.bytecode, state.GetState().SelectedFork);
                    EventsSink.EnqueueEvent(new RunBytecode(), true);
                    break;
                }
            case CallDataInserted ciMsg:
                {
                    var calldata = Nethermind.Core.Extensions.Bytes.FromHexString(ciMsg.calldata);
                    state.GetState().CallData = calldata;
                    break;
                }
            case UpdateState updState:
                {
                    if (updState.traces.Failed)
                    {
                        EventsSink.EnqueueEvent(new ThrowError($"Transaction Execution Failed"), true);
                        break;
                    }
                    return state.GetState().SetState(updState.traces);
                }
            case SetForkChoice frkMsg:
                {
                    context = new(frkMsg.forkName);
                    EventsSink.EnqueueEvent(new RunBytecode(), true);
                    return state.GetState().SetFork(frkMsg.forkName);
                }
            case SetGasMode gasMsg:
                {
                    state.GetState().SetGas(gasMsg.ignore ? int.MaxValue : gasMsg.gasValue);
                    EventsSink.EnqueueEvent(new RunBytecode(), true);
                    break;
                }
            case RunBytecode _:
                {
                    var localTracer = _tracer;
                    context.Execute(localTracer, state.GetState().AvailableGas, state.GetState().RuntimeContext.MachineCode);
                    EventsSink.EnqueueEvent(new UpdateState(localTracer.BuildResult()), true);
                    break;
                }
            case Reset _:
                {
                    return Initialize(state.GetState());
                }
            case ThrowError errMsg:
                {
                    throw new Exception(errMsg.error);
                }
        }
        return state;
    }

    private void HookKeyboardEvents()
    {
        MainPanel.KeyUp += (e) =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.F:
                    EventsSink.EnqueueEvent(new MoveNext());
                    break;

                case Key.B:
                    EventsSink.EnqueueEvent(new MoveBack());
                    break;
            }

        };
    }
}
