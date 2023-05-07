// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Tracing.DebugTrace;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class BytecodeView : IComponent<(DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec)>
{
    private bool isCached = false;
    private TabView? container = null;

    public void Dispose()
    {
        container?.Dispose();
        container?.Dispose();
    }

    public event Func<int, int, bool> BreakPointRequested;

    public (View, Rectangle?) View((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, Rectangle? rect = null)
    {
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

        ClearExistingTabs(container);
        if (state.RuntimeContext is EofCodeInfo eofRuntimeContext)
        {
            for(int i = 0; i < eofRuntimeContext._header.CodeSections.Length; i++)
            {
                (int start, int size) = eofRuntimeContext.SectionOffset(i);
                TableView programView = AddCodeSection(state, (true, i, eofRuntimeContext.CodeSection.Slice(start, size).ToArray()));
                container.AddTab(new TabView.Tab($"Section {i}", programView), i == 0);
            }
        }
        else
        {
            TableView programView = AddCodeSection(state, (false, 0, state.RuntimeContext.MachineCode));
            container.AddTab(new TabView.Tab("Section 0", programView), true);
        }

        return (container, frameBoundaries);
    }

    private void ClearExistingTabs(TabView view)
    {
        foreach(TabView.Tab tabView in view.Tabs.ToArray())
        {
            view.RemoveTab(tabView);
        }
    }

    private TableView AddCodeSection((DebugTracer txTracer, ICodeInfo RuntimeContext, IReleaseSpec Spec) state, (bool isEof, int index, byte[] bytecode) codeSection)
    {
        var dissassembledBytecode = BytecodeParser.Dissassemble(codeSection.isEof, codeSection.bytecode);

        var dataTable = new DataTable();
        dataTable.Columns.Add("    ");
        dataTable.Columns.Add("Position");
        dataTable.Columns.Add("Operation");
        int selectedRow = 0;

        foreach (var instr in dissassembledBytecode)
        {
            dataTable.Rows.Add(state.txTracer.BreakPoints.ContainsKey(instr.idx) ? "[v]" : "[ ]", instr.idx, instr.ToString(state.Spec));
        }

        var programView = new TableView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        programView.Table = dataTable;
        programView.SelectedRow = selectedRow;

        programView.SelectedCellChanged += e =>
        {
            if (BreakPointRequested?.Invoke(codeSection.index, dissassembledBytecode[e.NewRow].idx) ?? false)
            {
                dataTable.Rows[e.NewRow]["    "] = "[v]";
            }
            else
            {
                dataTable.Rows[e.NewRow]["    "] = "[ ]";
            }
        };
        return programView;
    }
}
