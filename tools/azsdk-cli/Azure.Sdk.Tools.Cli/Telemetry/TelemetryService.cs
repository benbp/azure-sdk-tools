// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Azure.Sdk.Tools.Cli.Configuration;
using Azure.Sdk.Tools.Cli.Telemetry.InformationProvider;
using Azure.Sdk.Tools.Cli.Extensions;
using Azure.Monitor.OpenTelemetry.Exporter;
using static Azure.Sdk.Tools.Cli.Telemetry.TelemetryConstants;

namespace Azure.Sdk.Tools.Cli.Telemetry;
/// <summary>
/// Provides access to services.
/// </summary>
internal class TelemetryService : ITelemetryService
{
    private readonly bool _isEnabled;
    private readonly List<KeyValuePair<string, object?>> _tagsList;
    private readonly IMachineInformationProvider _informationProvider;
    private readonly ILogger<TelemetryService> _logger;
    private readonly TaskCompletionSource _isInitialized = new TaskCompletionSource();

    internal ActivitySource Parent { get; }

    public TelemetryService(ILogger<TelemetryService> logger, IMachineInformationProvider informationProvider, IOptions<AzSdkToolsMcpServerConfiguration> options)
    {
        _isEnabled = options.Value.IsTelemetryEnabled;
        _tagsList = new List<KeyValuePair<string, object?>>()
        {
            new(TagName.AzSdkToolVersion, options.Value.Version),
        };

        Parent = new ActivitySource(options.Value.Name, options.Value.Version, _tagsList);

        _logger = logger;
        _informationProvider = informationProvider;

        Task.Factory.StartNew(InitializeTagList);
    }

    public static void RegisterCliTelemetry(IServiceCollection services, bool debug)
    {
        var telemetryEnv = Environment.GetEnvironmentVariable("AZSDKTOOLS_COLLECT_TELEMETRY");
        var telemetryEnabled = string.IsNullOrEmpty(telemetryEnv) || (bool.TryParse(telemetryEnv, out var parsed) && parsed);
        var appInsightsConnectionString = OpenTelemetryExtensions.GetAppInsightsConnectionString();

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());
                if (debug) { builder.AddConsoleExporter(); }
                if (telemetryEnabled)
                {
                    builder.AddAzureMonitorTraceExporter(options =>
                    {
#if DEBUG
                        options.EnableLiveMetrics = true;
                        options.Diagnostics.IsLoggingEnabled = true;
                        options.Diagnostics.IsLoggingContentEnabled = true;
#endif
                        options.ConnectionString = appInsightsConnectionString;
                    });
                }
            });
        //            .WithMetrics(m => m.AddMeter("Azure.Sdk.Tools.Cli.Metrics"));

        if (!telemetryEnabled)
        {
            return;
        }

        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry(logging =>
            {
                logging.AddAzureMonitorLogExporter(options =>
                {
                    options.ConnectionString = OpenTelemetryExtensions.GetAppInsightsConnectionString();
                });
            });
        });
    }

    public static void RegisterMcpServerTelemetry(IServiceCollection services, bool debug)
    {
        var builder = services.AddOpenTelemetry()
            .WithTracing(b =>
            {
                b.AddSource(Constants.TOOLS_ACTIVITY_SOURCE)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new TelemetryProcessor());
                if (debug) { b.AddConsoleExporter(); }
            })
            .WithMetrics(m => m.AddMeter("Azure.Sdk.Tools.Cli.Metrics"));
    }

    public ValueTask<Activity?> StartActivity(string activityId) => StartActivity(activityId, null);

    public async ValueTask<Activity?> StartActivity(string activityId, Implementation? clientInfo)
    {
        if (!_isEnabled)
        {
            return null;
        }

        await _isInitialized.Task;

        var activity = Parent.StartActivity(activityId, ActivityKind.Server);

        if (activity == null)
        {
#if DEBUG
            // Fail fast if we're generating null activities so we can catch silent issues in development
            throw new Exception($"Failed to start activity '{activityId}' as there is no listener registered");
#else
            return activity;
#endif
        }

        if (clientInfo != null)
        {
            activity.AddTag(TagName.ClientName, clientInfo.Name)
                .AddTag(TagName.ClientVersion, clientInfo.Version);
        }

        activity.AddTag(TagName.EventId, Guid.NewGuid().ToString());

        _tagsList.ForEach(kvp => activity.AddTag(kvp.Key, kvp.Value));

        return activity;
    }

    public void Dispose()
    {
    }

    private async Task InitializeTagList()
    {
        try
        {
            var macAddressHash = await _informationProvider.GetMacAddressHash();
            var deviceId = await _informationProvider.GetOrCreateDeviceId();

            _tagsList.Add(new(TagName.MacAddressHash, macAddressHash));
            _tagsList.Add(new(TagName.DevDeviceId, deviceId));
#if DEBUG
            _tagsList.Add(new(TagName.DebugTag, "true"));
#endif

            _isInitialized.SetResult();
        }
        catch (Exception ex)
        {
            _isInitialized.SetException(ex);
        }
    }
}
