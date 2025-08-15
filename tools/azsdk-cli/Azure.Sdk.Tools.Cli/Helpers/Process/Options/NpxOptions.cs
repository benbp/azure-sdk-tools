// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers;

public class NpxOptions : ProcessOptions, IProcessOptions
{
    private const string NPX = "npx";

    public string? Package { get; }

    private string shortName;
    public override string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(shortName))
            {
                shortName = string.IsNullOrEmpty(Package) ? "npx" : Package;
            }
            return shortName;
        }
    }

    public NpxOptions(
        string? package,
        string[] args,
        string? workingDirectory = null,
        bool logOutputStream = true,
        TimeSpan? timeout = null
    ) : this(BuildArgs(package, args), workingDirectory, logOutputStream, timeout)
    {
        Package = package;
    }

    private NpxOptions(
        string[] args,
        string? workingDirectory,
        bool logOutputStream,
        TimeSpan? timeout
    ) : base("npx", args, workingDirectory, logOutputStream, timeout) {}

    private static string[] BuildArgs(string? package, string[] args)
    {
        if (string.IsNullOrEmpty(package))
        {
            return args;
        }
        return [$"--package={package}", "--", .. args];
    }
}
