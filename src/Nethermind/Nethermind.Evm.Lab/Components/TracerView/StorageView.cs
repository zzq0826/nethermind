// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using Nethermind.Evm.Lab.Interfaces;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.TracerView;
internal class StorageView : IComponent<Dictionary<string, string>>
{
    bool isCached = false;
    private FrameView? container = null;
    private TableView? table = null;

    public void Dispose()
    {
        container?.Dispose();
        table?.Dispose();
    }

    public (View, Rectangle?) View(Dictionary<string, string> state, Rectangle? rect = null)
    {
        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );
        container ??= new FrameView("StorageState")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        var dataTable = new DataTable();
        dataTable.Columns.Add("Address");
        dataTable.Columns.Add("Value");
        if(state is not null)
        {
            foreach (var (k, v) in state)
            {
                dataTable.Rows.Add(k, v);
            }
        }

        table ??= new TableView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };
        table.Table = dataTable;
        if (!isCached)
        {
            container.Add(table);
        }
        isCached = true;
        return (container, frameBoundaries);
    }
}
