// SPDX-FileCopyrightText: 2023 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Config;

namespace Nethermind.Shutter;

public interface IShutterConfig : IConfig
{
    /// <summary>
    /// Gets or sets a value indicating whether the Shutter plugin is enabled.
    /// </summary>
    [ConfigItem(Description = "Defines whether the Shutter plugin is enabled.", DefaultValue = "false")]
    bool Enabled { get; set; }
}
