// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using MachineStateEvents;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Componants;
using Nethermind.Evm.Lab.Components.GlobalViews;
using Nethermind.Evm.Lab.Components.MachineLab;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Specs.Forks;
using Terminal.Gui;
namespace Nethermind.Evm.Lab.Components;

// Note(Ayman) : Add possibility to run multiple bytecodes at once using tabular views
internal class MachineView : IComponent<MachineState>
{
    private View MainPanel;
    public MachineState? defaultValue;
    public bool isCached = false;
    public IComponent<MachineState>[] _components;
    public MachineView()
    {
        _components = new IComponent<MachineState>[]{
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


    
    public (View, Rectangle?) View(IState<MachineState> state, Rectangle? rect = null)
    {
        IComponent<MachineState> _component_cpu = _components[0];
        IComponent<MachineState> _component_stk = _components[1];
        IComponent<MachineState> _component_ram = _components[2];
        IComponent<MachineState> _component_inpt = _components[3];
        IComponent<MachineState> _component_rtrn = _components[4];
        IComponent<MachineState> _component_strg = _components[5];
        IComponent<MachineState> _component_pgr = _components[6];
        IComponent<MachineState> _component_cnfg = _components[7];

        MainPanel ??= new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var (cpu_view, cpu_rect) = _component_cpu.View(state, new Rectangle(0, 0, Dim.Percent(30), Dim.Percent(25))); // h:10 w:30
        var (stack_view, stack_rect) = _component_stk.View(state, cpu_rect.Value with // h:50 w:30
        {
            Y = Pos.Bottom(cpu_view),
            Height = Dim.Percent(40)
        });
        var (ram_view, ram_rect) = _component_ram.View(state, stack_rect.Value with // h: 100, w:100
        {
            Y = Pos.Bottom(stack_view),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        });
        var (input_view, input_rect) = _component_inpt.View(state, cpu_rect.Value with // h: 10, w : 80
        {
            X = Pos.Right(cpu_view),
            Width = Dim.Percent(50)
        });
        var (storage_view, storage_rect) = _component_strg.View(state, input_rect.Value with // h: 40, w: 80
        {
            Y = Pos.Bottom(input_view),
            Height = Dim.Percent(25),
        });
        var (return_view, return_rect) = _component_rtrn.View(state, storage_rect.Value with
        {
            Y = Pos.Bottom(storage_view),
            Height = Dim.Percent(15),
        });
        var (config_view, config_rect) = _component_cnfg.View(state, input_rect.Value with
        {
            X = Pos.Right(input_view),
            Width = Dim.Percent(20)
        });
        var (program_view, program_rect) = _component_pgr.View(state, config_rect.Value with
        {
            Y = Pos.Bottom(config_view),
            Height = Dim.Percent(40),
        });

        if (!isCached)
        {
            HookKeyboardEvents(state);
            MainPanel.Add(program_view, config_view, return_view, storage_view, input_view, ram_view, stack_view, cpu_view);
        }
        isCached = true;
        return (MainPanel, null);
    }

    

    private void HookKeyboardEvents(IState<MachineState> state)
    {
        MainPanel.KeyUp += (e) =>
        {
            switch (e.KeyEvent.Key)
            {
                case Key.F:
                    state.EventsSink.EnqueueEvent(new MoveNext());
                    break;

                case Key.B:
                    state.EventsSink.EnqueueEvent(new MoveBack());
                    break;
            }

        };
    }

    public void Dispose()
    {
        MainPanel?.Dispose();
        if(_components != null)
            foreach(var comp in _components)
            {
                comp?.Dispose();
            }
    }
}
