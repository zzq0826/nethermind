// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.IO;
using System.Runtime.InteropServices;

namespace Nethermind.Db.Rocks;
internal class NativeLibraryHelpers
{
    public static string? GetLibraryLocation(string runtimesDirectoryLocation, string libraryName)
    {
        string platform =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "";

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => "-x86",
            Architecture.X64 => "-x64",
            Architecture.Arm64 => "-arm64",
            _ => "",
        };

        string[] extensions =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? [".so"] :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? [".dylib", ".so"] :
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? [".dll"] : [""];

        string[] prefixes =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ["lib", ""] :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? ["lib", ""] :
            [""];

        foreach (string extension in extensions)
        {
            foreach (string prefix in prefixes)
            {
                string fullPath = Path.Combine(runtimesDirectoryLocation, $"runtimes/{platform}{arch}/native/{prefix}{libraryName}{extension}");
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }
}
