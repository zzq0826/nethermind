// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.Differ;

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
