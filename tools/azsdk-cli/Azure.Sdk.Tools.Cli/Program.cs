// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Telemetry;
using ModelContextProtocol.Server;
using System.CommandLine.Invocation;

namespace Azure.Sdk.Tools.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var (outputFormat, debug) = SharedOptions.GetGlobalOptionValues(args);

        // Early parse to detect 'start'. We still want 'start --help' to show help, so we build the root command and add a start command stub.
        var rootCommand = CommandFactory.CreateRootCommand(args, BuildCliServiceProvider(outputFormat, debug), debug);

        var startCommand = new Command("start", "Starts the MCP server (stdio mode)")
        {
        };
        startCommand.SetHandler(async (InvocationContext ctx) =>
        {
            var code = await RunMcpServerAsync(args, outputFormat, debug, ctx.GetCancellationToken());
            ctx.ExitCode = code;
        });
        rootCommand.AddCommand(startCommand);

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler()
            .Build();

        // If user specified 'start', executing the handler will run the server; otherwise normal CLI path.
        return await parser.InvokeAsync(args);
    }

    private static IServiceProvider BuildCliServiceProvider(string outputFormat, bool debug)
    {
        var builder = Host.CreateApplicationBuilder();
        var logLevel = debug ? LogLevel.Debug : LogLevel.Information;
        builder.Logging.AddConsole();
        builder.Services.AddLogging(l => { l.AddConsole(); l.SetMinimumLevel(logLevel); });
        var outputMode = outputFormat switch
        {
            "plain" => OutputHelper.OutputModes.Plain,
            "json" => OutputHelper.OutputModes.Json,
            _ => throw new ArgumentException($"Invalid output format '{outputFormat}'. Supported formats are: plain, json")
        };
        ServiceRegistrations.RegisterCommonServices(builder.Services, outputMode);
        // Register CLI (not MCP) tools only; server tools are registered in server host.
        ServiceRegistrations.RegisterInstrumentedMcpTools(builder.Services, []);
        return builder.Build().Services;
    }

    private static async Task<int> RunMcpServerAsync(string[] args, string outputFormat, bool debug, CancellationToken ct)
    {
        var builder = Host.CreateApplicationBuilder(args);
        TelemetryService.RegisterServerTelemetry(builder.Services, debug);

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Debug; // all stderr for MCP
        });
        var logLevel = debug ? LogLevel.Debug : LogLevel.Information;
        builder.Logging.AddFilter((category, level) =>
        {
            if (debug || category == null)
            {
                return level >= logLevel;
            }
            var isAzureClient = category.StartsWith("Azure.", StringComparison.Ordinal);
            var isToolsClient = category.StartsWith("Azure.Sdk.Tools.", StringComparison.Ordinal);
            if (isAzureClient && !isToolsClient)
            {
                return level >= LogLevel.Error;
            }
            return level >= logLevel;
        });

        builder.Services.AddLogging(l => { l.AddConsole(); l.SetMinimumLevel(logLevel); });

        // Always MCP output mode inside server
        ServiceRegistrations.RegisterCommonServices(builder.Services, OutputHelper.OutputModes.Mcp);
        ServiceRegistrations.RegisterInstrumentedMcpTools(builder.Services, args);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport();

        var host = builder.Build();
        await host.RunAsync(ct);
        return 0;
    }
}
