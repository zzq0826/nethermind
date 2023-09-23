// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Int256;

namespace Nethermind.JsonRpc.Modules.Evm
{
    public record PresetAddress(Address Address, UInt256 Balance, UInt256 Nonce);

    [RpcModule(ModuleType.Evm)]
    public interface IEvmRpcModule : IRpcModule
    {
        [JsonRpcMethod(Description = "Triggers block production.", IsImplemented = true, IsSharable = false)]
        ResultWrapper<bool> evm_mine();
        void evm_takeSnapshot();
        ResultWrapper<GethLikeTxTrace> evm_runBytecode(byte[] input, long gas, long blockNbr, UInt256 timestamp, PresetAddress[] presetAccounts = null);
    }
}
