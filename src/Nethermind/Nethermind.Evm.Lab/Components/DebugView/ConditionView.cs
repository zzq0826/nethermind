// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text;
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
    private Point mousePosition;
    private ContextMenu contextMenu = new ContextMenu();
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
            Application.RootMouseEvent += Application_RootMouseEvent;
            conditionBox.KeyPress += (e) =>
            {
                if (e.KeyEvent.Key == Key.Enter && conditionBox.HasFocus)
                {
                    SubmitCondition();
                } else if(e.KeyEvent.KeyValue == '.')
                {
                    string getToken(string text)
                    {
                        int currentIndex = text.Length - 1;
                        while (Char.IsLetter(text[currentIndex]) || text[currentIndex] == '_') {
                            currentIndex--;
                        }

                        return text[(currentIndex + 1)..text.Length];
                    }
                    Point offsetCorrection = conditionBox.ScreenToView(0, 0);


                    ShowContextMenu(getToken((string)conditionBox.Text), Math.Abs(offsetCorrection.X) + conditionBox.CursorPosition, Math.Abs(offsetCorrection.Y));
                }
            };
            container.Add(conditionBox);
        }

        isCached = true;
        return (container, frameBoundaries);
    }

    public void SubmitCondition()
    {
        var condition = String.IsNullOrWhiteSpace((string)conditionBox.Text) ? null :
            CSharpScript.EvaluateAsync<Func<EvmState, bool>>((string)conditionBox.Text, ScriptOptions.Default.WithReferences(typeof(Nethermind.Evm.EvmState).Assembly)).Result;
        ActionRequested?.Invoke(new SetGlobalCheck(condition));
    }

    private void ShowContextMenu(string expandedToken, int x, int y)
    {
        var namedState = (string)conditionBox.Text.Substring(0, conditionBox.Text.IndexOf("=>")).TrimSpace();
        if(expandedToken == namedState)
        {
            conditionBox.Text += ".";
            Type evmstateType = typeof(EvmState);
            var props = evmstateType.GetProperties().Select(prop => prop.Name);
            contextMenu = new ContextMenu(x, y,
                new MenuBarItem(
                    props.Select(name => new MenuItem(name, string.Empty, () =>
                    {
                        conditionBox.Text += name;
                        conditionBox.CursorPosition += name.Length + 1;
                    })).ToArray()
            ))
            { ForceMinimumPosToZero = false, UseSubMenusSingleFrame = false};

            contextMenu.Show();
        }
    }


    void Application_RootMouseEvent(MouseEvent me)
    {
        mousePosition = new Point(me.X, me.Y);
    }
}
