// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm.Lab.Interfaces;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.TracerView;
internal class MemoryView : IComponent<byte[]>
{
    bool isCached = false;
    private FrameView? container = null;
    private HexView? memoryView = null;

    public void Dispose()
    {
        container?.Dispose();
        memoryView?.Dispose();
    }

    public (View, Rectangle?) View(byte[] ram, Rectangle? rect = null)
    {
        var streamFromBuffer = new MemoryStream(ram);

        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? 50,
                Height: rect?.Height ?? 10
            );
        container ??= new FrameView("MemoryState")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        memoryView ??= new HexView()
        {
            Width = Dim.Fill(2),
            Height = Dim.Fill(2),
        };
        memoryView.Source = streamFromBuffer;

        if (!isCached)
        {
            container.Add(memoryView);
        }
        isCached = true;
        return (container, frameBoundaries);
    }
}
