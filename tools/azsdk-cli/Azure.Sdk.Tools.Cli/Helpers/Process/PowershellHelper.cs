// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IPowershellHelper
    {
        public Task<ProcessResult> RunInline(List<string> args, string workingDirectory, CancellationToken ct);
        public Task<ProcessResult> RunScript(string scriptPath, List<string> args, string workingDirectory, CancellationToken ct);
        public Task<ProcessResult> Run(PowershellCommandOptions options, CancellationToken ct);
        public PowershellCommandOptions CreateCommandOptions(string? scriptPath = null, string cwd = default);
    }

    public sealed class PowershellCommandOptions
    {
        public string? ScriptPath { get; set; }
        public string Cwd { get; set; } = Environment.CurrentDirectory;
        public List<string> Args { get; } = [];

        public PowershellCommandOptions AddArgs(params string[] args)
        {
            this.Args.AddRange(args);
            return this;
        }

        public PowershellCommandOptions AddArgs(IEnumerable<string> args)
        {
            this.Args.AddRange(args);
            return this;
        }
    }

    public class PowershellHelper(IProcessHelper processHelper) : IPowershellHelper
    {
        public async Task<ProcessResult> RunInline(List<string> args, string workingDirectory, CancellationToken ct)
        {
            return await processHelper.RunProcess("pwsh", ["-Command", .. args], workingDirectory, ct);
        }

        public async Task<ProcessResult> RunScript(string scriptPath, List<string> args, string workingDirectory, CancellationToken ct)
        {
            return await processHelper.RunProcess("pwsh", ["-File", scriptPath, .. args], workingDirectory, ct);
        }

        public async Task<ProcessResult> Run(PowershellCommandOptions options, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(options.ScriptPath))
            {
                return await RunScript(options.ScriptPath, options.Args, options.Cwd, ct);
            }

            return await RunInline(options.Args, options.Cwd, ct);
        }

        public PowershellCommandOptions CreateCommandOptions(string? scriptPath = null, string cwd = default)
        {
            return new PowershellCommandOptions
            {
                ScriptPath = scriptPath,
                Cwd = string.IsNullOrEmpty(cwd) ? Environment.CurrentDirectory : cwd
            };
        }
    }
}
