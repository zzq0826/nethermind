// SPDX-FileCopyrightText:2023 Demerzel Solutions Limited
// SPDX-License-Identifier:LGPL-3.0-only

using Nethermind.State;

namespace Nethermind.Evm.Test.Verkle;

public class VerkleVirtualMachineTestsBase() : VirtualMachineTestsBase(StateType.Verkle);
