// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Logging;

namespace Nethermind.Shutter;

public sealed class ShutterPlugin : INethermindPlugin
{
    private IShutterConfig _config = null!;
    private ILogger _logger = default!;
    private ILogManager _logManager = default!;
    private INethermindApi _nethermindApi = default!;

    public ValueTask DisposeAsync() => default;

    public Task Init(INethermindApi nethermindApi)
    {
        ArgumentNullException.ThrowIfNull(nethermindApi);

        _nethermindApi = nethermindApi;
        _config = _nethermindApi.Config<IShutterConfig>();
        _logManager = _nethermindApi.LogManager;
        _logger = _logManager.GetClassLogger<ShutterPlugin>();

        return Task.CompletedTask;
    }

    public Task InitNetworkProtocol()
    {
        return Task.CompletedTask;
    }

    public Task InitRpcModules() => Task.CompletedTask;

    public string Author { get; } = "Nethermind";

    public string Description { get; } = "TODO";

    private bool Enabled => _config?.Enabled ?? false;

    public string Name { get; } = "Shutter";
}
