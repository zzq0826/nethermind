// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using Nethermind.Evm.Lab.Interfaces;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class StackView : IComponent<MachineState>
{
    bool isCached = false;
    private FrameView? container = null;
    private TableView? stackView = null;

    public void Dispose()
    {
        container?.Dispose();
        stackView?.Dispose();
    }

    public (View, Rectangle?) View(IState<MachineState> state, Rectangle? rect = null)
    {
        var innerState = state.GetState();
        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );
        container ??= new FrameView("StackState")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        stackView ??= new TableView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };

        var dataTable = new DataTable();
        dataTable.Columns.Add("Index");
        dataTable.Columns.Add("Value");

        int stringLen = 32;
        var cleanedUpDataSource = innerState.Current.Stack.Select(entry =>
        {
            entry = entry.StartsWith("0x") ? entry.Substring(2) : entry;
            if(entry.Length < stringLen)
            {
                entry = entry.PadLeft(stringLen - entry.Length, '0');
            } else
            {
                entry = entry.Substring(entry.Length - stringLen);
            }
            return entry;
        }).ToList();

        int index = 0;
        foreach (var value in cleanedUpDataSource)
        {
            dataTable.Rows.Add(index++, value);
        }
        stackView.Table = dataTable;

        if (!isCached)
            container.Add(stackView);
        isCached = true;
        return (container, frameBoundaries);
    }
}
