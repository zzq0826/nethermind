// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Reflection;
using MachineState.Actions;
using Nethermind.Core.Specs;
using Nethermind.Evm.Lab.Interfaces;
using Nethermind.Specs.Forks;
using NStack;
using Terminal.Gui;

namespace Nethermind.Evm.Lab.Components.MachineLab;

internal class FilteredInputField : TextField
{
    protected Func<ustring,bool> _filter = bool (ustring _) => true;
    public FilteredInputField(ustring defaultValue) : base(defaultValue.ToString()) { }
    public FilteredInputField(Func<ustring, bool> filter, ustring defaultValue) : base(defaultValue)
        => _filter = filter;

    public override TextChangingEventArgs OnTextChanging(ustring newText)
    {
        if (_filter(newText))
            return new TextChangingEventArgs(this.Text);
        return base.OnTextChanging(newText);
    }
}
internal class NumberInputField : FilteredInputField
{
    public NumberInputField(long defaultValue) : base(defaultValue.ToString())
    {
        _filter = bool (ustring s) => s.Where(c => Char.IsAsciiLetter((char)c)).Any();
    }

    public void AddFilter(Func<ustring, bool> filter)
    {
        _filter += filter; 
    }
}
