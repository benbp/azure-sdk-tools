// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;
using ModelContextProtocol.Server;

namespace Azure.Sdk.Tools.Cli.Tools;

[McpServerToolType, Description("Cleans up various engsys resources")]
public class CleanupTool(IAzureAgentServiceFactory agentServiceFactory, ILogger<CleanupTool> logger) : MCPTool
{
    public const string CleanupAgentsCommandName = "agents";

    public Option<string> projectEndpointOpt = new(["--project-endpoint", "-e"], "The AI foundry project to clean up") { IsRequired = false };

    public override Command GetCommand()
    {
        Command command = new("cleanup", "Cleanup commands");
        var cleanupCommand = new Command(CleanupAgentsCommandName, "Cleanup ai agents") { projectEndpointOpt };

        cleanupCommand.SetHandler(async ctx => { await HandleCommand(ctx, ctx.GetCancellationToken()); });
        command.AddCommand(cleanupCommand);

        return command;
    }

    public override async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
    {
        if (ctx.ParseResult.CommandResult.Command.Name != CleanupAgentsCommandName)
        {
            logger.LogError("Unknown command: {command}", ctx.ParseResult.CommandResult.Command.Name);
            SetFailure();
            return;
        }
        var projectEndpoint = ctx.ParseResult.GetValueForOption(projectEndpointOpt);
        await CleanupAgents(projectEndpoint);
    }

    [McpServerTool, Description("Clean up AI agents in an AI foundry project. Leave projectEndpoint empty if not specified")]
    public async Task CleanupAgents(string? projectEndpoint = null)
    {
        try
        {
            var agentService = agentServiceFactory.Create(projectEndpoint, null);
            await agentService.DeleteAgents();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up agents in project {ProjectName}.", projectEndpoint);
            SetFailure();
        }
    }
}