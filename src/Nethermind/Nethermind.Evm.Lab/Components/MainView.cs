// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using GlobalStateEvents.Actions;
using Nethermind.Evm.Lab.Components.GlobalViews;
using Nethermind.Evm.Lab.Interfaces;
using Terminal.Gui;
namespace Nethermind.Evm.Lab.Components;

// Note(Ayman) : Add possibility to run multiple bytecodes at once using tabular views
internal class MainView : IComponent<GlobalState>
{
    private List<PageView> pages = new();
    private bool isCached;
    private Window container;
    private TabView table;
    private HeaderView header;
    private PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
    public GlobalState State = new GlobalState();
    public MainView()
    {
        header = new HeaderView();
    }

    private void AddMachinePage(MachineState? state = null, string name = null)
    {
        var pageObj = new PageView();
        var pageState = state ?? new MachineState();
        pages.Add(pageObj);
        State.MachineStates.Add(pageState);
        if (state is null)
        {
            pageState.Initialize(true);
        }
        table.AddTab(new TabView.Tab(name ?? "Default", pageObj.View(pageState).Item1), true);
    }

    private void RemoveMachinePage(int idx)
    {
        int index = 0;
        if(State.MachineStates.Count > 1)
        {
            TabView.Tab targetTab = null;
            foreach (var tab in table.Tabs)
            {
                if (idx == index)
                {
                    targetTab = tab;
                    break;
                }
                index++;
            }
            table.RemoveTab(targetTab);
            pages.RemoveAt(index);
            State.MachineStates.RemoveAt(index);
        }
    }

    private int GetTabIndex(TabView.Tab page)
    {
        int index = 0;
        foreach (var tab in table.Tabs)
        {
            if(tab == page)
            {
                return index;
            }
            index++;
        }
        throw new UnreachableException();
    }

    private void UpdateTabPage(View page, int idx)
    {
        int index = 0;
        foreach(var tab in table.Tabs)
        {
            if(idx == index)
            {
                tab.View = page;
                break;
            }
        }

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

    public async Task<bool> MoveNext(GlobalState state)
    {
        if (state.EventsSink.TryDequeueEvent(out var currentEvent))
        {
            lock (state)
            {
                try
                {
                    state = Update(state, currentEvent).GetState();
                }
                catch (Exception ex)
                {
                    var dialogView = MainView.ShowError(ex.Message,
                        () =>
                        {
                            state.EventsSink.EnqueueEvent(new Reset());
                        }
                    );
                }
            }
            return true;
        }
        return false;
    }

    public async Task Run(GlobalState state)
    {
        bool firstRender = true;
        Application.Init();
        Application.MainLoop.Invoke(
            async () =>
            {
                if(firstRender)
                {
                    firstRender = false;
                    Application.Top.Add(header.View(state).Item1, View(state).Item1);
                }

                do {

                    await MoveNext(state);
                    for (int i = 0; i < pages.Count; i++)
                    {
                        if(pages[i].IsSelected() && await State.MachineStates[i].MoveNext())
                        {
                            UpdateTabPage(pages[i].View(State.MachineStates[i]).Item1, i);
                        }
                    }
                }
                while (firstRender || await timer.WaitForNextTickAsync()) ;
            });

        Application.Run();
        Application.Shutdown();
    }
    public (View, Rectangle?) View(IState<GlobalState> state, Rectangle? rect = null)
    {
        var innerState = state.GetState();
        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? Dim.Fill(),
                Height: rect?.Height ?? Dim.Percent(10)
            );

        container ??= new Window("EvmLaboratory")
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.TopLevel
        };

        table ??= new TabView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.TopLevel
        };

        if(!isCached)
        {
            table.SelectedTabChanged += (s, e) => state.GetState().SelectedState = GetTabIndex(e.NewTab);
            AddMachinePage();
            container.Add(table);
            isCached = true;
        }
        return (container, frameBoundaries);
    }

    public IState<GlobalState> Update(IState<GlobalState> state, ActionsBase msg)
    {
        switch (msg)
        {
            case AddPage msgA:
                {
                    AddMachinePage(name: msgA.name);
                    break;
                }
            case RemovePage msgR:
                {
                    RemoveMachinePage(state.GetState().SelectedState);
                    break;
                }
        }

        return state;
    }
}
