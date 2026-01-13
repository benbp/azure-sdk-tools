// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Runtime.InteropServices;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Telemetry.InformationProvider;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Azure.Sdk.Tools.Cli.Telemetry;

internal enum TelemetryMode
{
    Cli,
    McpServer,
}

internal static class TelemetryRegistration
{
    private const string DefaultAppInsightsConnectionString = "InstrumentationKey=cf8756d3-ef86-4365-9da3-c3df9d28b1d3;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=9dd94d04-d58f-4d70-b9b2-40682cd7b3e7";

    internal static void AddTelemetry(this IServiceCollection services, TelemetryMode mode, bool debug)
    {
        ConfigureTelemetryOptions(services);
        RegisterMachineInformationProvider(services);
        services.AddSingleton<ITelemetryService, TelemetryService>();

        var telemetryEnabled = IsTelemetryEnabled();

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(ConfigureResource)
            .WithTracing(builder =>
            {
                builder.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());

                if (mode == TelemetryMode.McpServer)
                {
                    builder.AddAspNetCoreInstrumentation();
                }

                if (debug)
                {
                    builder.AddConsoleExporter();
                }

                if (telemetryEnabled)
                {
                    builder.AddAzureMonitorTraceExporter(options =>
                    {
#if DEBUG
                        options.EnableLiveMetrics = true;
                        options.Diagnostics.IsLoggingEnabled = true;
                        options.Diagnostics.IsLoggingContentEnabled = true;
#endif
                        options.ConnectionString = GetAppInsightsConnectionString();
                    });
                }
            });

        if (mode == TelemetryMode.McpServer)
        {
            openTelemetry.WithMetrics(builder =>
            {
                builder.AddMeter("Azure.Sdk.Tools.Cli.Metrics");

                if (telemetryEnabled)
                {
                    builder.AddAzureMonitorMetricExporter(options =>
                    {
#if DEBUG
                        options.EnableLiveMetrics = true;
                        options.Diagnostics.IsLoggingEnabled = true;
                        options.Diagnostics.IsLoggingContentEnabled = true;
#endif
                        options.ConnectionString = GetAppInsightsConnectionString();
                    });
                }
            });
        }
    }

    private static void ConfigureTelemetryOptions(IServiceCollection services)
    {
        services.AddOptions<AzSdkToolsMcpServerConfiguration>()
            .Configure(options =>
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                var assemblyName = entryAssembly?.GetName() ?? new AssemblyName();
                if (assemblyName.Version != null)
                {
                    options.Version = assemblyName.Version.ToString();
                }

                options.IsTelemetryEnabled = IsTelemetryEnabled();
            });
    }

    private static void RegisterMachineInformationProvider(IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<IMachineInformationProvider, WindowsMachineInformationProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<IMachineInformationProvider, MacOSXMachineInformationProvider>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddSingleton<IMachineInformationProvider, LinuxMachineInformationProvider>();
        }
        else
        {
            services.AddSingleton<IMachineInformationProvider, DefaultMachineInformationProvider>();
        }
    }

    private static void ConfigureResource(ResourceBuilder resource)
    {
        var version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString();
        resource.AddService(Constants.TOOLS_ACTIVITY_SOURCE, version)
            .AddTelemetrySdk();
    }

    private static string GetAppInsightsConnectionString()
    {
        var appInsightsConnectionString = Environment.GetEnvironmentVariable("AZSDKTOOLS_APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (string.IsNullOrEmpty(appInsightsConnectionString))
        {
            appInsightsConnectionString = DefaultAppInsightsConnectionString;
        }

        return appInsightsConnectionString;
    }

    private static bool IsTelemetryEnabled()
    {
        var telemetryEnv = Environment.GetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY");
        return string.IsNullOrEmpty(telemetryEnv)
            || (bool.TryParse(telemetryEnv, out var parsed) && parsed);
    }
}
