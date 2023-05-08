// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using System.Security.Cryptography.Xml;
using DebuggerStateEvents;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Components.Differ;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Tracing.DebugTrace;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class BytecodeView : IComponent<(DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec)>
{
    private bool isCached = false;
    private TabView? container = null;
    private ICodeInfo cachedRuntimeContext;
    public void Dispose()
    {
        container?.Dispose();
        container?.Dispose();
    }

    public event Action<ActionsBase> BreakPointRequested;

    public (View, Rectangle?) View((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, Rectangle? rect = null)
    {
        bool shouldRerender = cachedRuntimeContext != state.RuntimeContext;
        cachedRuntimeContext = state.RuntimeContext;

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

        if (!isCached || shouldRerender)
        {
            ClearExistingTabs(container);
            if (state.RuntimeContext is EofCodeInfo eofRuntimeContext)
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
                (_, TableView programView) = AddCodeSectionTab(state, (false, 0, state.RuntimeContext.MachineCode, 0));
                container.AddTab(new TabView.Tab("Section 0", programView), true);
            }
        } else
        {
            foreach (var tab in container.Tabs)
            {
                UpdateCodeSectionTab(state, tab);
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

    private void UpdateCodeSectionTab((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, TabView.Tab page)
    {
        TableViewColored content = (TableViewColored)page.View;
        for(int i = 0; i < content.Table.Rows.Count; i++)
        {
            if (state.txTracer.BreakPoints.ContainsKey(Int32.Parse((string)content.Table.Rows[i]["Position"])))
            {
                content.Table.Rows[i]["    "] = "[v]";
                content.ColoredRanges.Add(new Range(i, i + 1));
            }
            else
            {
                content.Table.Rows[i]["    "] = "[ ]";
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
            dataTable.Rows.Add(line, state.txTracer.BreakPoints.ContainsKey(instr.idx) ? "[v]" : "[ ]", instr.idx, instr.ToString(state.Spec));
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
            BreakPointRequested?.Invoke(new SetBreakpoint(dissassembledBytecode[e.NewRow].idx));
        };
        if(selectedRow is not null)
        {
            programView.SelectedRow = selectedRow.Value;
        }
        return (selectedRow is not null, programView);
    }
}
