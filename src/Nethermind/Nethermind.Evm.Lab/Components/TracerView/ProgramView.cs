// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using Nethermind.Core.Extensions;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.TracerView;
internal class ProgramView : IComponent<MachineState>
{
    private bool isExternalSource = false;
    private bool isCached = false;
    private FrameView? container = null;
    private TableView? programView = null;

    public ProgramView(bool isExternalSource)
    {
        this.isExternalSource = isExternalSource;
    }

    public void Dispose()
    {
        container?.Dispose();
        programView?.Dispose();
    }

    public event Func<int, bool> BreakPointRequested;
    private HashSet<int> breakpoints = new();

    public (View, Rectangle?) View(MachineState state, Rectangle? rect = null)
    {
        var dissassembledBytecode = BytecodeParser.Dissassemble(state.RuntimeContext is EofCodeInfo, state.RuntimeContext.CodeSection.Span, isExternalSource);

        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );
        container ??= new FrameView("ProgramState")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        var dataTable = new DataTable();
        dataTable.Columns.Add("    ");
        dataTable.Columns.Add("Position");
        dataTable.Columns.Add("Operation");
        int selectedRow = 0;

        foreach (var instr in dissassembledBytecode)
        {
            dataTable.Rows.Add("[ ]", instr.idx, instr.ToString(state.SelectedFork));
            selectedRow += instr.idx < (isExternalSource ? state.Index : state.Entries[state.Index].Pc) ? 1 : 0;
        }

        programView ??= new TableView()
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
            if (BreakPointRequested?.Invoke(e.NewRow) ?? false)
            {
                dataTable.Rows[e.NewRow]["    "] = "[v]";
            }
            else
            {
                dataTable.Rows[e.NewRow]["    "] = "[ ]";
            }
        };

        if (!isCached)
        {
            container.Add(programView);
        }
        isCached = true;
        return (container, frameBoundaries);
    }
}
