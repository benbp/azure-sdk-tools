// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/*
 * This file contains types for running sub-processes that require custom options classes
 * used to modify the command line before being run.
 * This is all just boilerplate so we can get named logs via ILogger<T> and add
 * more clarity to the caller about what type of process is being run based on the helper type.
*/

namespace Azure.Sdk.Tools.Cli.Helpers;

public sealed class ProcessHelper : ProcessHelperBase<ProcessHelper, ProcessOptions>, IProcessHelper<ProcessOptions>
{
    public ProcessHelper(ILogger<ProcessHelper> logger) : base(logger) { }
    public new async Task<ProcessResult> Run(ProcessOptions options, CancellationToken ct) => await base.Run(options, ct);
}

public sealed class PowershellHelper : ProcessHelperBase<PowershellHelper, PowershellOptions>, IProcessHelper<PowershellOptions>
{
    public PowershellHelper(ILogger<PowershellHelper> logger) : base(logger) { }
    public new async Task<ProcessResult> Run(PowershellOptions options, CancellationToken ct) => await base.Run(options, ct);
}

public sealed class NpxHelper : ProcessHelperBase<NpxHelper, NpxOptions>, IProcessHelper<NpxOptions>
{
    public NpxHelper(ILogger<NpxHelper> logger) : base(logger) { }
    public new async Task<ProcessResult> Run(NpxOptions options, CancellationToken ct) => await base.Run(options, ct);
}
