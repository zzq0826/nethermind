// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Data;
using MachineStateEvents;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Evm.Tracing.GethStyle;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.DebugView;
internal class MachineOverview : IComponent<MachineState>
{
    bool isCached = false;
    private FrameView? container = null;
    private (TableView? generalState, TableView? opcodeData) machinView = (null, null);

    private static readonly string[] Columns_Overview = { "Pc", "GasAvailable", "GasUsed", "Depth", "Error" };
    private static readonly string[] Columns_Opcode = { "Opcode", "Operation", "GasCost" };
    public IState<MachineState> Update(IState<MachineState> currentState, ActionsBase action)
    {
        var innerState = currentState.GetState();
        return action switch
        {
            MoveNext => innerState?.Next(),
            MoveBack _ => innerState?.Previous(),
            Goto act => innerState?.Goto(act.index),
            _ => currentState
        };
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
        container ??= new FrameView("ProcessorState")
        {
            X = frameBoundaries.X,
            Y = frameBoundaries.Y,
            Width = frameBoundaries.Width,
            Height = frameBoundaries.Height,
        };

        var dataTable = new DataTable();
        foreach (var h in Columns_Overview)
        {
            dataTable.Columns.Add(h);
        }

        dataTable.Rows.Add(
            Columns_Overview.Select(propertyName =>
            propertyName switch
            {
                "GasUsed" => innerState.AvailableGas - innerState.Current.Gas - GasCostOf.Transaction,
                "GasAvailable" => innerState.Current.Gas,
                _ => typeof(GethTxTraceEntry).GetProperty(propertyName)?.GetValue(innerState.Current)
            }).ToArray()
        );

        var opcodeData = new DataTable();
        foreach (var h in Columns_Opcode)
        {
            opcodeData.Columns.Add(h);
        }
        opcodeData.Rows.Add(
            Columns_Opcode.Select(
                proeprtyName =>
                {
                    if (proeprtyName == "Opcode")
                    {
                        var opcodeName = innerState.Current.Operation;
                        var Instruction = (byte)Enum.Parse<Evm.Instruction>(opcodeName);
                        return (Object?)$"{Instruction:X4}";
                    }
                    else
                    {
                        return typeof(GethTxTraceEntry).GetProperty(proeprtyName)?.GetValue(innerState.Current);
                    }
                }
            ).ToArray()
        );

        machinView.generalState ??= new TableView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(2),
            Height = Dim.Percent(50),
        };
        machinView.generalState.Table = dataTable;

        machinView.opcodeData ??= new TableView()
        {
            X = 0,
            Y = Pos.Bottom(machinView.generalState),
            Width = Dim.Fill(2),
            Height = Dim.Percent(50),
        };
        machinView.opcodeData.Table = opcodeData;

        if (!isCached)
        {
            container.Add(machinView.generalState, machinView.opcodeData);
        }
        isCached = true;
        return (container, frameBoundaries);
    }

    public void Dispose()
    {
        container?.Dispose();
    }
}
