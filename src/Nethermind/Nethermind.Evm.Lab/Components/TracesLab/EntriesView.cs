// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Components;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Tracing.GethStyle;
using Terminal.Gui;
using static Microsoft.FSharp.Core.ByRefKinds;

namespace Nethermind.Evm.Lab.Componants;
class TableViewColored : TableView
{
    public int DiffIndexStart = 2;
    public int RenderCellIndex = 0;
    public bool RenderPastDiffLine = false;
    public override void Redraw(Rect bounds)
    {
        RenderCellIndex = 0;
        RenderPastDiffLine = false;
        base.Redraw(bounds);
    }

    protected override void RenderCell(Terminal.Gui.Attribute cellColor, string render, bool isPrimaryCell)
    {
        int lineIndex = 0;
        RenderPastDiffLine |= RenderCellIndex % 8 == 0
            && Int32.TryParse(render, out lineIndex) ? lineIndex >= DiffIndexStart : false;
        for (int i = 0; i < render.Length; i++)
        {
            if (RenderPastDiffLine)
            {
                if (RenderCellIndex % 8 == 0)
                {
                    Driver.SetAttribute(Driver.MakeAttribute(lineIndex == SelectedRow ? Color.Brown: Color.Magenta, cellColor.Background));
                }
                else
                {
                    Driver.SetAttribute(Driver.MakeAttribute(Color.Red, cellColor.Background));
                }
            } else {
                Driver.SetAttribute(cellColor);
            }

            Driver.AddRune(render[i]);
        }
        RenderCellIndex++;
    }
}
internal class EntriesView : IComponent<MachineState>
{
    bool isCached = false;
    private TableViewColored? programView = null;
    private static string[] properties = new string[] { "Step", "Pc", "Operation", "Opcode", "GasCost", "Gas", "Depth", "Error" };
    private int startDiffColoringAt;
    public EntriesView(int DiffingIndex) => startDiffColoringAt = DiffingIndex;
    public (View, Rectangle?) View(IState<MachineState> state, Rectangle? rect = null)
    {
        var innerState = state.GetState();

        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );

        if(!isCached)
        {

            var dataTable = new DataTable();

            foreach (var prop in properties)
            {
                dataTable.Columns.Add(prop);
            }

            int RowIndex = 0;
            foreach (var entry in innerState.Entries)
            {
                var opcode = Enum.Parse<Evm.Instruction>(entry.Operation);
                dataTable.Rows.Add(RowIndex++, $"0x{entry.Pc:X4}", opcode.ToString(), (int)opcode, entry.GasCost, entry.Gas, entry.Depth, entry.Error);
            }

            programView ??= new TableViewColored()
            {
                X = frameBoundaries.X,
                Y = frameBoundaries.Y,
                Width = frameBoundaries.Width,
                Height = frameBoundaries.Height,
            };

            programView.Table = dataTable;

            isCached = true;
        }
        programView.RenderCellIndex = 0;
        programView.DiffIndexStart = startDiffColoringAt;

        programView.SelectedRow = innerState.Index;

        return (programView, frameBoundaries);
    }

    public void Dispose()
    {
        programView?.Dispose();
    }
}
