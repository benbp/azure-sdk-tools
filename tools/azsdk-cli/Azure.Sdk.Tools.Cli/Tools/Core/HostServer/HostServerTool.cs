// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tools.HostServer
{
    public class HostServerTool(ILogger<HostServerTool> logger, IRawOutputHelper outputHelper)
    {
        public Command GetCommand()
        {
            Command cmd = new("start", "Starts the MCP server (stdio mode)");
            cmd.SetHandler(async ctx => await HandleCommand(ctx, ctx.GetCancellationToken()));
            return cmd;
        }

        public async Task HandleCommand(InvocationContext ctx, CancellationToken ct)
        {
            try
            {
                await Program.ServerApp.RunAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception during web app run");
                ctx.ExitCode = 1;
                outputHelper.OutputConsoleError($"Exception during web app run: {ex.Message}");
            }
        }
    }
}
