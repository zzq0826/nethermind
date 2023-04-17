// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
internal class HalfPageView : IComponent<MachineState>
{
    bool isCached = false;
    private FrameView? container = null;
    private MachineOverview? processorView = null;
    private EntriesView? entriesView = null;
    private StackView? stackView = null;
    private MemoryView? memoView = null;

    private string TitleName = string.Empty;
    public HalfPageView(string titleName)
    {
        processorView ??= new MachineOverview();
        entriesView ??= new EntriesView();
        stackView ??= new StackView();
        memoView ??= new MemoryView();
        TitleName = titleName;
    }

    public void Dispose()
    {
        processorView?.Dispose();
        entriesView?.Dispose();
        stackView?.Dispose();
        memoView?.Dispose();
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

        container ??= new FrameView(TitleName)
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };


        var (cpu_view, cpu_rect) = processorView.View(state, new Rectangle(0, 0, Dim.Fill(), Dim.Percent(30))); // h:10 w:30
        var (entries_view, entries_rect) = entriesView.View(state, cpu_rect.Value with // h:50 w:30
        {
            Y = Pos.Bottom(cpu_view),
            Height = Dim.Percent(30)
        });
        var (stack_view, stack_rect) = stackView.View(state, entries_rect.Value with // h:50 w:30
        {
            Y = Pos.Bottom(entries_view),
            Height = Dim.Percent(25)
        });
        var (memory_view, memory_rect) = memoView.View(state, stack_rect.Value with // h:50 w:30
        {
            Y = Pos.Bottom(stack_view),
            Height = Dim.Percent(25)
        });

        if (!isCached)
        {
            container.Add(cpu_view, entries_view, stack_view, memory_view);
            isCached = true;
        }

        return (container, frameBoundaries);
    }
}
