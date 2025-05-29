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

    public Option<string> subscriptionIdOpt = new(["--subscription", "-s"], "The Azure subscription ID to use");
    public Option<string> resourceGroupOpt = new(["--resource-group", "-g"], "The Azure resource group to target") { IsRequired = true };
    public Option<string> accountNameOpt = new(["--account-name", "-a"], "The ai services account to clean up") { IsRequired = false };
    public Option<string> projectNameOpt = new(["--project-name", "-p"], "The AI foundry project/ML workspace to clean up") { IsRequired = false };

    public override Command GetCommand()
    {
        Command command = new("cleanup", "Cleanup commands");
        var cleanupCommand = new Command(CleanupAgentsCommandName, "Cleanup ai agents") { subscriptionIdOpt, resourceGroupOpt, accountNameOpt, projectNameOpt };

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
        var subscriptionId = ctx.ParseResult.GetValueForOption(subscriptionIdOpt);
        var resourceGroup = ctx.ParseResult.GetValueForOption(resourceGroupOpt);
        var accountName = ctx.ParseResult.GetValueForOption(accountNameOpt);
        var projectName = ctx.ParseResult.GetValueForOption(projectNameOpt);
        await CleanupAgents(subscriptionId, resourceGroup, accountName, projectName, ct);
    }

    public async Task CleanupAgents(string subscriptionId, string resourceGroup, string accountName, string projectName, CancellationToken ct)
    {
        var agentService = await agentServiceFactory.Create(subscriptionId, resourceGroup, accountName, projectName, ct);
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
            logger.LogWarning("Ensure you have the 'Cognitive Services Contributor' role for the AI Foundry project {ProjectName}.", projectName);
            SetFailure();
        }
    }
}