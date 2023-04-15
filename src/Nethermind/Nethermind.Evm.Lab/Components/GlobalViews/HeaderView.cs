// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using GlobalStateEvents.Actions;
using MachineStateEvents;
using Nethermind.Consensus.Tracing;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Tracing.GethStyle;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.GlobalViews;
internal class HeaderView : IComponent<GlobalState>
{
    private bool IsCached = false;
    private MenuBar menu;
    
    public (View, Rectangle?) View(IState<GlobalState> state, Rectangle? rect = null)
    {
        if(!IsCached)
        {
            menu ??= new MenuBar(new MenuBarItem[] {
                new MenuBarItem ("_File", new MenuItem [] {
                    new MenuItem ("_Run", "", () => {
                        using var fileOpenDialogue = new OpenDialog("Bytecode File", "Select a binary file that contains EVM bytecode");
                        fileOpenDialogue.Closed += (e) =>
                        {
                            if(fileOpenDialogue.Canceled) return;
                            var filePath = (string)fileOpenDialogue.FilePath;
                            var contentAsText = File.ReadAllText (filePath);
                            var innerState = state.GetState();
                            var substate = innerState.MachineStates[innerState.SelectedState];
                            substate.EventsSink.EnqueueEvent(new BytecodeInserted(contentAsText));
                        };
                        Application.Run(fileOpenDialogue);
                    }),
                    new MenuItem ("_Open", "", () => {
                        using var fileOpenDialogue = new OpenDialog("Trace File", "Select a Traces file that contains EVM bytecode");
                        fileOpenDialogue.Closed += (e) =>
                        {
                            if(fileOpenDialogue.Canceled) return;
                            var filePath = (string)fileOpenDialogue.FilePath;
                            var fileName = Path.GetFileName (filePath);
                            var contentAsText = File.ReadAllText (filePath);
                            try {
                                GethLikeTxTrace? traces = JsonSerializer.Deserialize<GethLikeTxTrace>(contentAsText);
                                if(traces is not null)
                                {
                                    state.EventsSink.EnqueueEvent(new AddPage(fileName, new MachineState(traces)));
                                    return;
                                }
                                else goto  error_section;
                            } catch
                            {
                                goto  error_section;
                            }
error_section:              MainView.ShowError("Failed to deserialize Traces Provided!");
                        };
                        Application.Run(fileOpenDialogue);
                    }),
                    new MenuItem ("_Export", "", () => {
                        // open trace file
                        using var saveOpenDialogue = new SaveDialog("Bytecode File", "Select a binary file that contains EVM bytecode");
                        saveOpenDialogue .Closed += (e) =>
                        {
                            if(saveOpenDialogue.Canceled) return;
                            var filePath = (string)saveOpenDialogue.FilePath;
                            var localState = state.GetState();
                            var serializedData = System.Text.Json.JsonSerializer.Serialize(localState.MachineStates[localState.SelectedState] as GethLikeTxTrace);
                            File.WriteAllText(filePath, serializedData);
                        };
                        Application.Run(saveOpenDialogue);
                    }),
                    new MenuItem ("_Quit", "", () => {
                        Application.RequestStop ();
                    })
                }),

                new MenuBarItem ("_Action", new MenuItem [] {
                    new MenuItem ("_New", "", () => {
                        state.EventsSink.EnqueueEvent(new AddPage($"Page {state.GetState().MachineStates.Count}"));
                    }),
                    new MenuItem ("_Remove", "", () => {
                        state.EventsSink.EnqueueEvent(new RemovePage(state.GetState().SelectedState));
                    }),
                }),
            });
            IsCached = true;
        }

        return (menu, null);
    }
}
