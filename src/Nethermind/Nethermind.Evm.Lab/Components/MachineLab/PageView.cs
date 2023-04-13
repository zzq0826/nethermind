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
internal class PageView : IComponent<MachineState>
{
    private FrameView MainPanel;
    public MachineState? defaultValue;
    public bool isCached = false;
    public IComponent<MachineState>[] _components;
    public PageView()
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

        MainPanel ??= new FrameView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

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
        {
            HookKeyboardEvents(state);
            MainPanel.Add(view1, view4, view2, view3, view5, view6, view7, view8);
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

    public bool IsSelected() => MainPanel.HasFocus;
}
