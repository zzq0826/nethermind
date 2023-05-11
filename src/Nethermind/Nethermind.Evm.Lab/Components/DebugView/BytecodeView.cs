// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using System.Security.Cryptography.Xml;
using DebuggerStateEvents;
using MachineStateEvents;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Components.Differ;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Tracing.DebugTrace;
using Nethermind.Specs;
using Terminal.Gui;
using static Microsoft.FSharp.Core.ByRefKinds;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class BytecodeView : IComponent<(DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec)>
{
    private bool isCached = false;
    private ContextMenu contextMenu = new ContextMenu();
    private TabView? container = null;
    private Point mousePosition;
    private ICodeInfo cachedRuntimeContext;
    public void Dispose()
    {
        container?.Dispose();
        container?.Dispose();
    }

    public event Action<ActionsBase> BreakPointRequested;

    public (View, Rectangle?) View((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, Rectangle? rect = null)
    {
        var codeInfo = state.txTracer.CurrentState?.Env.CodeInfo ?? state.RuntimeContext;
        bool shouldRerender = cachedRuntimeContext != codeInfo;
        cachedRuntimeContext = codeInfo;

        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );
        container ??= new TabView()
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        if(!isCached )
        {
            Application.RootMouseEvent += Application_RootMouseEvent;
        }

        if (!isCached || shouldRerender)
        {
            ClearExistingTabs(container);
            if (codeInfo is EofCodeInfo eofRuntimeContext)
            {
                for (int i = 0; i < eofRuntimeContext._header.CodeSections.Length; i++)
                {
                    (int start, int size) = eofRuntimeContext.SectionOffset(i);
                    (bool isSelected, TableView programView) = AddCodeSectionTab(state, (true, i, eofRuntimeContext.CodeSection.Slice(start, size).ToArray(), start));
                    container.AddTab(new TabView.Tab($"Section {i}", programView), isSelected);
                }
            }
            else
            {
                (_, TableView programView) = AddCodeSectionTab(state, (false, 0, codeInfo.MachineCode, 0));
                container.AddTab(new TabView.Tab("Section 0", programView), true);
            }
        } else
        {
            foreach (var tab in container.Tabs)
            {
                UpdateCodeSectionTab(state, (container, tab));
            }
        }
        isCached = true;
        return (container, frameBoundaries);
    }

    private void ClearExistingTabs(TabView view)
    {
        foreach(TabView.Tab tabView in view.Tabs.ToArray())
        {
            view.RemoveTab(tabView);
        }
    }

    private void UpdateCodeSectionTab((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, (TabView parent, TabView.Tab page) container)
    {
        TableViewColored content = (TableViewColored)container.page.View;
        content.ClearColoredRegions();
        content.HighlightRow(-1);
        for (int i = 0; i < content.Table.Rows.Count; i++)
        {
            int pc = Int32.Parse((string)content.Table.Rows[i]["Position"]);
            if (pc == state.txTracer.CurrentState?.ProgramCounter)
            {
                content.HighlightRow(i);
                container.parent.SelectedTab = container.page;
            }

            if (state.txTracer._breakPoints.ContainsKey(pc))
            {
                content.Table.Rows[i][1] = "[x]";
                content.ColoredRanges.Add(new Range(i, i + 1));
            }
            else
            {
                content.Table.Rows[i][1] = "[ ]";
                content.ColoredRanges.Remove(new Range(i, i + 1));
            }
        }
    }


    private (bool, TableView) AddCodeSectionTab((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, (bool isEof, int index, byte[] bytecode, int sectionOffset) codeSection)
    {
        var dissassembledBytecode = BytecodeParser.Dissassemble(codeSection.isEof, codeSection.bytecode, offsetInstructionIndexesBy: codeSection.sectionOffset);

        var dataTable = new DataTable();
        dataTable.Columns.Add("idx");
        dataTable.Columns.Add("    ");
        dataTable.Columns.Add("Position");
        dataTable.Columns.Add("Operation");
        int? selectedRow = null;

        int line = 0;
        foreach (var instr in dissassembledBytecode)
        {
            string opcode = instr.ToString(state.Spec);
            dataTable.Rows.Add(line, state.txTracer._breakPoints.ContainsKey(instr.idx) ? "[x]" : "[ ]", instr.idx, $"{(opcode.Length > 13 ? $"{opcode.Substring(0, 13)}..." : opcode)}");
            if(instr.idx == (state.txTracer?.CurrentState?.ProgramCounter ?? 0))
            {
                selectedRow = line;
            }
            line++;
        }

        var programView = new TableViewColored()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        programView.Table = dataTable;
        programView.LineLenght = 4;
        programView.OverrideLineIndex += (table, word) =>
        {
            int lineIndex = 0;
            if (table.RenderCellIndex % table.LineLenght == 0
                && Int32.TryParse(word, out lineIndex))
            {
                return lineIndex;
            }
            else return null;
        };

        programView.SelectedCellChanged += e =>
        {
            int pc = dissassembledBytecode[e.NewRow].idx;
            BreakPointRequested?.Invoke(new SetBreakpoint(pc, unsetBreakpoint: state.txTracer.IsBreakpoitnSet(pc)));
        };
        if(selectedRow is not null)
        {
            programView.SelectedRow = selectedRow.Value;
        }

        programView.MouseClick += (e) => {
            var cell = programView.ScreenToCell(e.MouseEvent.X, e.MouseEvent.Y);

            if(cell is not null && cell.Value.X == 1)
            {
                int pc = Int32.Parse((string)programView.Table.Rows[cell.Value.Y]["Position"]);
                if (e.MouseEvent.Flags == contextMenu.MouseFlags)
                {
                    ShowContextMenu(pc, mousePosition.X, mousePosition.Y, state.txTracer.IsBreakpoitnSet(pc));
                    e.Handled = true;
                }
            }

        };

        return (selectedRow is not null, programView);
    }

    private void ShowContextMenu(int pc, int x, int y, bool isAlreadySet)
    {
        Dialog CreateConditionView()
        {
            var submit = new Button("Submit");
            var cancel = new Button("Cancel");
            Dialog container = new Dialog("Condition Input", 35, 6, submit, cancel)
            {
                X = x,
                Y = y,
            };

            ConditionView conditionView = new ConditionView(
                (msg) => BreakPointRequested?.Invoke(new SetBreakpoint(pc, (msg as SetGlobalCheck).condition, unsetBreakpoint: false))
            );
            conditionView.AutocompleteOn = false;

            submit.Clicked += () =>
            {
                conditionView.SubmitCondition();
                Application.RequestStop();
            };

            cancel.Clicked += () =>
            {
                Application.RequestStop();
            };

            container.Add(conditionView.View(new Rectangle
            {
                Height = 3, Width = 33
            }).Item1);
            return container;
        }


        contextMenu = new ContextMenu(x, y,
            new MenuBarItem(new MenuItem[] {
                    isAlreadySet
                        ? new MenuItem ("_RemoveBreakpoint", string.Empty, () => BreakPointRequested?.Invoke(new SetBreakpoint(pc, unsetBreakpoint: true)))
                        : new MenuItem ("_AddBreakpoint", string.Empty, () => BreakPointRequested?.Invoke(new SetBreakpoint(pc, unsetBreakpoint: false))),
                    isAlreadySet
                        ? new MenuBarItem ("_Conditions", new MenuItem [] {
                            new MenuItem ("_AddCondition", string.Empty, () => Application.Run(CreateConditionView())),
                            new MenuItem ("_RemoveCondition", string.Empty, () => BreakPointRequested?.Invoke(new SetBreakpoint(pc, unsetBreakpoint: false)))
                        })
                        : null,
            })
        )
        { ForceMinimumPosToZero = true, UseSubMenusSingleFrame = true};


        contextMenu.Show();
    }


	void Application_RootMouseEvent(MouseEvent me)
    {
        mousePosition = new Point(me.X, me.Y);
    }
}
