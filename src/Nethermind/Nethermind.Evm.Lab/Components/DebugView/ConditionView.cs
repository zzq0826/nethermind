// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using DebuggerStateEvents;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Nethermind.Evm.Lab.Interfaces;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class ConditionView : IComponent
{
    bool isCached = false;
    private FrameView? container = null;
    private TextField conditionBox = null;
    public ConditionView(Action<ActionsBase> actionHandler = null)
    {
        ActionRequested += actionHandler;
    }
    public void Dispose()
    {
        container?.Dispose();
    }

    public event Action<ActionsBase> ActionRequested;

    public (View, Rectangle?) View(Rectangle? rect = null)
    {
        var frameBoundaries = new Rectangle(
                X: rect?.X ?? 0,
                Y: rect?.Y ?? 0,
                Width: rect?.Width ?? Dim.Fill(),
                Height: rect?.Height ?? Dim.Percent(10)
            );
        container ??= new FrameView("Condition Field")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        conditionBox ??= new TextField()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };


        if (!isCached)
        {
            conditionBox.KeyPress += (e) =>
            {
                if (e.KeyEvent.Key == Key.Enter)
                {
                    var condition = CSharpScript.EvaluateAsync<Func<EvmState, bool>>((string)conditionBox.Text, ScriptOptions.Default.WithReferences(typeof(Nethermind.Evm.EvmState).Assembly)).Result;
                    ActionRequested?.Invoke(new SetGlobalCheck(condition));
                }
            };
            container.Add(conditionBox);
        }

        isCached = true;
        return (container, frameBoundaries);
    }
}
