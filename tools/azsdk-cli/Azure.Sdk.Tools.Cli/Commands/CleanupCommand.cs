// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Contract;

namespace Azure.Sdk.Tools.Cli.Commands;

public class CleanupCommand(IAzureAgentServiceFactory agentServiceFactory, ILogger<CleanupCommand> logger) : MCPTool
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
        await CleanupAgents(projectEndpoint, ct);
    }

    public async Task CleanupAgents(string projectEndpoint, CancellationToken ct)
    {
        var agentService = agentServiceFactory.Create(projectEndpoint, null);
        if (agentService == null)
        {
            logger.LogError("Failed to create agent service client. Please check the provided subscription ID, resource group, and project name.");
            SetFailure();
            return;
        }

        try
        {
            await agentService.DeleteAgents();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up agents.");
            logger.LogWarning("Ensure you have the 'Cognitive Services Contributor' role for the AI Foundry project {ProjectName}.", agentService.ProjectEndpoint);
            SetFailure();
        }
    }
}