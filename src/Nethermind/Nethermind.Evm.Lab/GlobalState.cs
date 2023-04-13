// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm.Lab.Interfaces;



namespace GlobalStateEvents.Actions
{
    public record AddPage(string name) : ActionsBase;
    public record RemovePage(int index) : ActionsBase;
    public record Reset : ActionsBase;

}

namespace Nethermind.Evm.Lab
{

    internal class GlobalState : IState<GlobalState>
    {
        public static string initialCmdArgument;
        public EventsSink EventsSink { get; } = new EventsSink();

        public List<MachineState> MachineStates = new List<MachineState>();
        IState<GlobalState> IState<GlobalState>.Initialize(GlobalState seed) => seed;
        public int SelectedState { get; set; }
    }
}
