// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.Lab;
using Nethermind.Evm.Lab.Componants;
using Nethermind.Evm.Lab.Components;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Specs.Forks;
using Terminal.Gui;

GlobalState.initialCmdArgument = args.Length == 0 ? "604260005260206000F3" : args[0];
var mainView = new MainView();
mainView.Run(mainView.State);
