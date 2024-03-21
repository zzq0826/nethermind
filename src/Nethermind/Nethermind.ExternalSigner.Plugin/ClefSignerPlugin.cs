// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;

// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api.Extensions;
using Nethermind.Core;
using Nethermind.JsonRpc.Client;
using Nethermind.JsonRpc;
using Nethermind.Network;
using Nethermind.Consensus;
using Nethermind.KeyStore.Config;

namespace Nethermind.ExternalSigner.Plugin;

public class ClefSignerPlugin : INethermindPlugin
{
    private INethermindApi? _nethermindApi;

    public string Name => throw new NotImplementedException();

    public string Description => throw new NotImplementedException();

    public string Author => throw new NotImplementedException();

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public Task Init(INethermindApi nethermindApi)
    {
        _nethermindApi = nethermindApi ?? throw new ArgumentNullException(nameof(nethermindApi));
        return Task.CompletedTask;
    }

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }

    public async Task InitRpcModules()
    {
        if (_nethermindApi == null)
            throw new InvalidOperationException("Init() must be called first.");

        IMiningConfig miningConfig = _nethermindApi.Config<IMiningConfig>();
        if (miningConfig.Enabled)
        {
            if (!string.IsNullOrEmpty(miningConfig.Signer))
            {
                ClefSigner signerAndStore =
                    await SetupExternalSigner(miningConfig.Signer, _nethermindApi.Config<IKeyStoreConfig>().BlockAuthorAccount);
                _nethermindApi.EngineSigner = signerAndStore;
            }
        }
    }

    private async Task<ClefSigner> SetupExternalSigner(string urlSigner, string blockAuthorAccount)
    {
        try
        {
            Address? address = string.IsNullOrEmpty(blockAuthorAccount) ? null : new Address(blockAuthorAccount);
            BasicJsonRpcClient rpcClient = new(new Uri(urlSigner), _nethermindApi!.EthereumJsonSerializer, _nethermindApi.LogManager, TimeSpan.FromSeconds(10));
            _nethermindApi.DisposeStack.Push(rpcClient);
            return await ClefSigner.Create(rpcClient, address);
        }
        catch (HttpRequestException e)
        {
            throw new NetworkingException($"Remote signer at {urlSigner} did not respond.", NetworkExceptionType.TargetUnreachable, e);
        }
    }
}
