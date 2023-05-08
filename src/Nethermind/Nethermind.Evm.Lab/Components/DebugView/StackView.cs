// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using Nethermind.Core.Extensions;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Int256;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class StackView : IComponent<(byte[] memory, int height)>
{
    bool isCached = false;
    private FrameView? container = null;
    private TableView? stackView = null;
    private HexView? rawStackView = null;
    private TabView? viewsAggregator = null;
    private (Button Push, Button Pop) Actions;
    public void Dispose()
    {
        container?.Dispose();
        stackView?.Dispose();
        viewsAggregator?.Dispose();
        rawStackView?.Dispose();
    }
    public event Action<long, byte> ByteEdited;
    public event Action<int> StackHeightChangeRequest;
    public (View, Rectangle?) View((byte[] memory, int height) stack, Rectangle? rect = null)
    {
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

        var Uint256Stack = stack.memory
            ?.Take(32 * stack.height).Chunk(32)
             .Select(chunk => new UInt256(chunk, true))
             .Reverse();

        viewsAggregator ??= new TabView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(92),
        };

        Actions.Push ??= new Button("Push")
        {
            Y = Pos.Bottom(viewsAggregator),
            X = Pos.X(viewsAggregator),
            Height = Dim.Percent(7),
        };


        Actions.Pop ??= new Button("Pop")
        {
            X = Pos.Right(Actions.Push),
            Y = Pos.Bottom(viewsAggregator),
            Height = Dim.Percent(7),
        };


        AddClassicalStackView(Uint256Stack);
        AddRawStackView(stack.memory);

        if (!isCached)
        {
            var normalStackTab = new TabView.Tab("Stack", stackView);
            var rawStackTab = new TabView.Tab("Stack Memory", rawStackView);

            Actions.Push.Clicked += () => StackHeightChangeRequest?.Invoke(1);
            Actions.Pop.Clicked += () => StackHeightChangeRequest?.Invoke(-1);

            viewsAggregator.AddTab(rawStackTab, false);
            viewsAggregator.AddTab(normalStackTab, true);
            container.Add(viewsAggregator, Actions.Pop, Actions.Push);
        }
        isCached = true;
        return (container, frameBoundaries);
    }

    private void AddRawStackView(byte[] state)
    {
        var streamFromBuffer = new MemoryStream(state);
        rawStackView ??= new HexView()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        rawStackView.Source = streamFromBuffer;

        rawStackView.Edited += (e) =>
        {
            ByteEdited?.Invoke(e.Key, e.Value);
        };

    }

    private void AddClassicalStackView(IEnumerable<UInt256>? state)
    {
        stackView ??= new TableView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var dataTable = new DataTable();
        dataTable.Columns.Add("Index");
        dataTable.Columns.Add("Value");

        int stringLen = 32;
        var cleanedUpDataSource = state.Select(entry =>
        {

            string entryStr = entry.ToHexString(false);
            if (entryStr.Length < stringLen)
            {
                entryStr = entryStr.PadLeft(stringLen - entryStr.Length, '0');
            }
            else
            {
                entryStr = entryStr.Substring(entryStr.Length - stringLen);
            }
            return entryStr;
        }).ToList();

        int index = 0;
        foreach (var value in cleanedUpDataSource)
        {
            dataTable.Rows.Add(index++, value);
        }
        stackView.Table = dataTable;
    }
}
