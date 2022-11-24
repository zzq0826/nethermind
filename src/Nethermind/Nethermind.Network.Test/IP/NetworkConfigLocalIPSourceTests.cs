// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Nethermind.Logging;
using Nethermind.Network.Config;
using Nethermind.Network.IP;
using NUnit.Framework;

namespace Nethermind.Network.Test.IP;

public class NetworkConfigLocalIPSourceTests
{
    [TestCase("auto", false)]
    [TestCase("0.0.0.0", true)]
    [TestCase("127.0.0.1", true)]
    [TestCase("randomstring", false)]
    public async Task Resolve_if_ip(string configuredIp, bool isResolved)
    {
        NetworkConfig config = new()
        {
            LocalIp = configuredIp,
        };
        NetworkConfigLocalIPSource localIpSource = new(config, LimboLogs.Instance);

        (bool resolved, IPAddress _) = await localIpSource.TryGetIP();

        resolved.Should().Be(isResolved);
    }
}
