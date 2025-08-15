// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IPowershellHelper
    {
        public Task<ProcessResult> Run(List<string> args, string workingDirectory, CancellationToken ct);
        public IPowershellCommand CreateCommand();
        public IPowershellCommand CreateCommand(string scriptPath);
    }

    public interface IPowershellCommand
    {
        string? ScriptPath { get; set; }
        string Cwd { get; set; }
        IPowershellCommand AddArgs(params string[] args);
        IPowershellCommand AddArgs(IEnumerable<string> args);
        Task<ProcessResult> Run(CancellationToken ct);
    }

    public class PowershellCommand : IPowershellCommand
    {
        private readonly IPowershellHelper powershellHelper;
        private readonly List<string> args = [];

        public string? ScriptPath { get; set; }
        public string Cwd { get; set; } = Environment.CurrentDirectory;

        internal PowershellCommand(IPowershellHelper powershellHelper)
        {
            this.powershellHelper = powershellHelper;
        }

        public IPowershellCommand AddArgs(params string[] args)
        {
            this.args.AddRange(args);
            return this;
        }

        public IPowershellCommand AddArgs(IEnumerable<string> args)
        {
            this.args.AddRange(args);
            return this;
        }

        public async Task<ProcessResult> Run(CancellationToken ct)
        {
            var finalArgs = new List<string>();

            if (!string.IsNullOrEmpty(ScriptPath))
            {
                finalArgs.Add($"-File {ScriptPath}");
            }

            finalArgs.AddRange(args);

            return await powershellHelper.Run(finalArgs, Cwd, ct);
        }
    }

    public class PowershellHelper(IProcessHelper processHelper) : IPowershellHelper
    {
        public async Task<ProcessResult> Run(List<string> args, string workingDirectory, CancellationToken ct)
        {
            return await processHelper.RunProcess("pwsh", [.. args], workingDirectory, ct);
        }

        public IPowershellCommand CreateCommand()
        {
            return new PowershellCommand(this);
        }

        public IPowershellCommand CreateCommand(string scriptPath)
        {
            return new PowershellCommand(this) { ScriptPath = scriptPath };
        }
    }
}
