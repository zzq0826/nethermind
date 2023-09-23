// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Jint.Native;
using Nethermind.Int256;
using Nethermind.JsonRpc.Modules.Evm;

namespace Nethermind.Cli.Modules
{
    [CliModule("evm")]
    public class EvmCliModule : CliModuleBase
    {
        [CliProperty("evm", "runBytecode")]
        public long runBytecode(byte[] input, long gas, long blockNbr, UInt256 timestamp, PresetAddress[]? presetAccounts = null) => NodeManager.Post<long>("evm_runBytecode", input, gas, blockNbr, timestamp, presetAccounts).Result;

        public EvmCliModule(ICliEngine cliEngine, INodeManager nodeManager) : base(cliEngine, nodeManager)
        {
        }
    }
}
