// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Telemetry;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Tools.Core;

/// <summary>
/// This is the base class defining how an MCP enabled tool will interface with the server.
///
/// This covers:
///     - route registration/disambiguation
///     - compilation trim avoidance for reflection-included MCP tools
/// </summary>
public abstract class MCPToolBase
{
    private bool initialized = false;
    private IOutputHelper output { get; set; }
    private ITelemetryService telemetryService { get; set; }

    public Command? Command;

    public virtual CommandGroup[] CommandHierarchy { get; set; } = [];

    public int ExitCode { get; set; } = 0;

    public void Initialize(IOutputHelper outputHelper, ITelemetryService telemetryService)
    {
        this.telemetryService = telemetryService;
        initialized = true;
    }

    public void SetFailure(int exitCode = 1)
    {
        ExitCode = exitCode;
    }

    public async Task InstrumentedCommandHandler(Command command, InvocationContext ctx)
    {
        if (!initialized)
        {
            throw new InvalidOperationException("Tool must be initialized with Initialize() before use");
        }

        // TODO: add client info
        using var activity = await telemetryService.StartActivity(ActivityName.CommandExecuted);
        Activity.Current = activity;

        try
        {
            var fullCommandName = string.Join('.', command.Parents.Reverse().Select(p => p.Name).Append(command.Name));
            var commandLine = ctx.ParseResult.Tokens.ToString();
            activity?.AddTag(TagName.CommandName, fullCommandName);
            activity?.SetTag(TagName.CommandArgs, commandLine);

            var response = await HandleCommand(ctx, ctx.GetCancellationToken());
            var result = output.Format(response);
            ctx.ExitCode = response.ExitCode;

            activity?.SetTag(TagName.CommandResponse, result);

            if (response.ExitCode == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error);
            }

            output.Output(result);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public abstract List<Command> GetCommandInstances();

    public abstract Task<Models.Response> HandleCommand(InvocationContext ctx, CancellationToken ct);
}
