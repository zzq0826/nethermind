// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using MachineStateEvents;
using Nethermind.Core.Extensions;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Lab.Parser;
using Terminal.Gui;
using static Nethermind.Evm.Test.EofTestsBase;

namespace Nethermind.Evm.Lab.Components.MachineLab;
internal class MnemonicInput : IComponent<MachineState>
{
    private class CodeSection
    {
        public CodeSection(int iCount, int oCount, int sHeight)
            => (inCount, outCount, stackMax) = (iCount, oCount, sHeight);
        public int inCount = 0;
        public int outCount = 0;
        public int stackMax = 0;
        public string Body = string.Empty;
    }
    // keep view static and swap state instead 
    bool isCached = false;
    private Dialog? container = null;
    private CheckBox? eofModeSelection= null;
    private List<CodeSection>? sectionsField= null;
    private TabView? tabView = null;
    private (Button submit, Button cancel) buttons;
    private (Button add, Button remove) actions;
    public event Action<byte[]> BytecodeChanged;
    bool isEofMode = false;


    public void Dispose()
    {
        container?.Dispose();
        eofModeSelection?.Dispose();
        tabView?.Dispose();
        buttons.cancel?.Dispose();
        buttons.submit?.Dispose();
        actions.add?.Dispose();
        actions.remove?.Dispose();
    }


    private TextView CreateNewFunctionPage(bool select = true)
    {
        if (sectionsField is null || tabView is null || sectionsField.Count == 23)
            throw new System.Diagnostics.UnreachableException();

        var newCodeSection = new CodeSection(0, 0, 0);
        var container = new View()
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.Menu
        };

        var inLabel = new Terminal.Gui.Label("Inputs Count")
        {
            Width = Dim.Percent(30),
            ColorScheme = Colors.TopLevel
        };
        var inputCountField = new NumberInputField(0)
        {
            X = Pos.X(inLabel),
            Y = Pos.Bottom(inLabel),
            Width = Dim.Percent(30),
            Height = Dim.Percent(10),
            ColorScheme = Colors.TopLevel
        };
        inputCountField.AddFilter((_) => !isEofMode);
        inputCountField.TextChanged += (e) =>
        {
            if (Int32.TryParse((string)inputCountField.Text, out int value)) {
                newCodeSection.inCount = value;
            } else newCodeSection.inCount = 0;
        };

        var outLabel = new Terminal.Gui.Label("Outputs Count")
        {
            X = Pos.Right(inLabel) + 2,
            Width = Dim.Percent(30),
            ColorScheme = Colors.TopLevel
        };
        var outputCountField = new NumberInputField(0)
        {
            X = Pos.X(outLabel),
            Y = Pos.Bottom(outLabel),
            Width = Dim.Percent(30),
            Height = Dim.Percent(10),
            ColorScheme = Colors.TopLevel
        };
        outputCountField.AddFilter((_) => !isEofMode);
        outputCountField.TextChanged += (e) =>
        {
            if (!isEofMode) return;
            if (Int32.TryParse((string)outputCountField.Text, out int value)) {
                newCodeSection.outCount = value;
            } else newCodeSection.outCount = 0;
        };

        var maxLabel = new Terminal.Gui.Label("Max Stack Height")
        {
            X = Pos.Right(outLabel) + 2,
            Width = Dim.Percent(30),
            ColorScheme = Colors.TopLevel
        };
        var stackHeightField = new NumberInputField(0)
        {
            X = Pos.X(maxLabel),
            Y = Pos.Bottom(maxLabel),
            Width = Dim.Percent(30),
            Height = Dim.Percent(10),
            ColorScheme = Colors.TopLevel
        };
        stackHeightField.AddFilter((_) => !isEofMode);
        stackHeightField.TextChanged += (e) => {
            if (!isEofMode) return;
            if(Int32.TryParse((string)stackHeightField.Text, out int value)) {
                newCodeSection.stackMax = value;
            } else newCodeSection.stackMax = 0;
        };

        var inputBodyField = new Terminal.Gui.TextView
        {
            Y = Pos.Bottom(stackHeightField),
            Width = Dim.Fill(),
            Height = Dim.Percent(100),
            ColorScheme = Colors.Base
        };
        inputBodyField.Initialized += (s, e) =>
        {
            newCodeSection.Body = (string)inputBodyField.Text;
        };

        inputBodyField.KeyPress += (e) =>
        {
            newCodeSection.Body = (string)inputBodyField.Text;
        };

        container.Add(
            inLabel, outLabel, maxLabel,
            inputCountField,
            outputCountField,
            stackHeightField,
            inputBodyField
        );

        var currentTab = new TabView.Tab($"{sectionsField.Count}", container);
        sectionsField.Add(newCodeSection);
        tabView.AddTab(currentTab, select);
        return inputBodyField;
    }

    private void RemoveSelectedFunctionPage()
    {
        if (sectionsField is null || tabView is null || sectionsField.Count == 1)
            return;

        int indexOf = tabView.Tabs.ToList().IndexOf(tabView.SelectedTab); // ugly code veeeeeery ugly
        sectionsField.RemoveAt(indexOf);
        tabView.RemoveTab(tabView.SelectedTab);

        int idx = 0;
        foreach (var tab in tabView.Tabs)
        {
            tab.Text = (idx++).ToString();
        }
    }

    private void SubmitBytecodeChanges(IState<MachineState> state, bool isEofContext, IEnumerable<CodeSection> functionsBytecodes)
    {
        byte[] bytecode = Array.Empty<byte>();
        if(!isEofContext)
        {
            bytecode = BytecodeParser.Parse(sectionsField[0].Body.Trim()).ToByteArray();
        } else
        {
            var scenario = new ScenarioCase(sectionsField.Select(field => new FunctionCase(field.inCount, field.outCount, field.stackMax, BytecodeParser.Parse(field.Body.Trim()).ToByteArray())).ToArray(), Array.Empty<byte>());
            bytecode = scenario.Bytecode;
        }
        BytecodeChanged?.Invoke(bytecode);
        state.EventsSink.EnqueueEvent(new BytecodeInsertedB(bytecode), true);
    } 


    public (View, Rectangle?) View(IState<MachineState> state, Rectangle? rect = null)
    {
        var innerState = state.GetState();


        var frameBoundaries = new Rectangle(
                X: rect?.X ?? Pos.Center(),
                Y: rect?.Y ?? Pos.Center(),
                Width: rect?.Width ?? Dim.Percent(25),
                Height: rect?.Height ?? Dim.Percent(75)
            );

        eofModeSelection ??= new CheckBox("Is Eof Mode Enabled", innerState.SelectedFork.IsEip3540Enabled)
        {
            Width = Dim.Fill(),
            Height = Dim.Percent(5),
            Checked = isEofMode
        };

        tabView ??= new TabView()
        {
            Y = Pos.Bottom(eofModeSelection),
            Width = Dim.Fill(),
            Height = Dim.Percent(95),
        };

        if (innerState.RuntimeContext is EofCodeInfo)
        {
            var eofCodeInfo = (EofCodeInfo)innerState.RuntimeContext;
            sectionsField = new List<CodeSection>(eofCodeInfo._header.CodeSections.Length);
            for(int i = 0; i <  eofCodeInfo._header.CodeSections.Length; i++)
            {
                var bodyInputFieldRef = CreateNewFunctionPage(i == 0);
                var codeSectionOffsets = eofCodeInfo._header.CodeSections[i];
                var bytecodeMnemonics = BytecodeParser.Dissassemble(true, innerState.RuntimeContext.MachineCode[codeSectionOffsets.Start..codeSectionOffsets.EndOffset])
                    .ToMultiLineString(innerState.SelectedFork);
                bodyInputFieldRef.Text = bytecodeMnemonics;
            }
        } else
        {
            sectionsField = new List<CodeSection>();
            var bodyInputFieldRef = CreateNewFunctionPage();
            var bytecodeMnemonics = BytecodeParser.Dissassemble(false, innerState.RuntimeContext.CodeSection.Span)
                .ToMultiLineString(innerState.SelectedFork);
            bodyInputFieldRef.Text = bytecodeMnemonics;
        }

        actions.add ??= new Button("Add");
        actions.remove ??= new Button("Remove");
        buttons.submit ??= new Button("Submit");
        buttons.cancel ??= new Button("Cancel");
        container ??= new Dialog("Bytecode Insertion View", 100, 7, actions.add, actions.remove, buttons.submit, buttons.cancel)
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
            ColorScheme = Colors.TopLevel
        };

        if (!isCached)
        {
            container.Add(eofModeSelection, tabView); 
            buttons.submit.Clicked += () =>
            {
                try
                {

                    if (!isEofMode && sectionsField.Count > 1)
                        throw new Exception("Cannot have more than one code section in non-Eof code");

                    SubmitBytecodeChanges(state, isEofMode, sectionsField);
                    Application.RequestStop();
                } catch (Exception ex)
                {
                    MainView.ShowError(ex.Message);
                }
            };

            eofModeSelection.Toggled += (e) =>
            {
                eofModeSelection.Checked = isEofMode = eofModeSelection.Checked && innerState.SelectedFork.IsEip3540Enabled;
            };

            buttons.cancel.Clicked += () =>
            {
                Application.RequestStop();
            };

            actions.add.Clicked += () => CreateNewFunctionPage(true);

            actions.remove.Clicked += RemoveSelectedFunctionPage;
        }
        isCached = true;

        return (container, frameBoundaries);
    }

}
