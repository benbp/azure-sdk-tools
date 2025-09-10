// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;

namespace Azure.Sdk.Tools.Cli.Contract;

/// <summary>
/// This is the base class defining how an MCP enabled tool will interface with the server.
///
/// This covers:
///     - route registration/disambiguation
///     - compilation trim avoidance for reflection-included MCP tools
/// </summary>
public abstract class MCPToolBase
{
    public MCPToolBase() { }

    public Command? Command;

    public int ExitCode { get; set; } = 0;

    public void SetFailure(int exitCode = 1)
    {
        ExitCode = exitCode;
    }

    public abstract List<Command> GetCommandInstances();

    public virtual CommandGroup[] CommandHierarchy { get; set; } = [];

    public abstract Task HandleCommand(InvocationContext ctx, CancellationToken ct);
}
