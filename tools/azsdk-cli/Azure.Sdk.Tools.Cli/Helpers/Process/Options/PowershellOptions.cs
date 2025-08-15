// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Runtime.InteropServices;

namespace Azure.Sdk.Tools.Cli.Helpers;

public class PowershellOptions : ProcessOptions, IProcessOptions
{
    public string? ScriptPath { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(ScriptPath) ? "pwsh" : ScriptPath;
            }
            return shortName;
        }
    }

    public PowershellOptions(
        string[] args,
        string? workingDirectory = null,
        bool logOutputStream = true,
        TimeSpan? timeout = null
    ) : base("pwsh", ["-Command", ..args], workingDirectory, logOutputStream, timeout) {}

    public PowershellOptions(
        string scriptPath,
        string[] args,
        string? workingDirectory = null,
        bool logOutputStream = true,
        TimeSpan? timeout = null
    ) : base("pwsh", ["-File", scriptPath, ..args], workingDirectory, logOutputStream, timeout)
    {
        ScriptPath = scriptPath;
    }
}
