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
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Nethermind.Evm.Tracing.GethStyle;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Componants;
internal class EntriesView : IComponent<MachineState>
{
    bool isCached = false;
    private TableView? programView = null;
    private static string[] properties = new string[] { "Step", "Pc", "Operation", "Opcode", "GasCost", "Gas", "Depth", "Error" };
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
                dataTable.Rows.Add(RowIndex++, $"0x{entry.Pc:X4}", entry.Operation, (int)Enum.Parse<Evm.Instruction>(entry.Operation), entry.GasCost, entry.Gas, entry.Depth, entry.Error);
            }

            programView ??= new TableView()
            {
                X = frameBoundaries.X,
                Y = frameBoundaries.Y,
                Width = frameBoundaries.Width,
                Height = frameBoundaries.Height,
            };
            programView.Table = dataTable;


            isCached = true;
        }
        programView.SelectedRow = innerState.Index;
        return (programView, frameBoundaries);
    }

    public void Dispose()
    {
        programView?.Dispose();
    }
}
