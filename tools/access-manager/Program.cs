using System;
using System.CommandLine;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

var fileOption = new Option<FileInfo?>(name: "--file", description: "Path to access config file for identities");

var rootCommand = new RootCommand("RBAC and Federated Identity manager for Azure SDK apps");
rootCommand.AddOption(fileOption);

rootCommand.SetHandler(async (file) =>
    {
        try
        {
            await Run(file!);
        }
        catch (ODataError ex)
        {
            Console.WriteLine(ex.Error?.Code);
            Console.WriteLine(ex.Error?.Message);
        }
    },
    fileOption);

await rootCommand.InvokeAsync(args);

static async Task Run(FileInfo config)
{
    Console.WriteLine("Using config -> " + config.FullName + Environment.NewLine);

    var accessConfig = new AccessConfig(config.FullName);
    accessConfig.Initialize();
    Console.WriteLine(accessConfig.ToString());

    foreach (var cfg in accessConfig.ApplicationAccessConfigs!)
    {
        await Sync(cfg);
    }
}

static async Task Sync(ApplicationAccessConfig appAccessConfig)
{
    Console.WriteLine($"Syncing {appAccessConfig.AppDisplayName}");

    var graphClient = new GraphServiceClient(new DefaultAzureCredential());

    var result = await graphClient.Applications.GetAsync((requestConfiguration) =>
    {
        requestConfiguration.QueryParameters.Search = $"\"displayName:{appAccessConfig.AppDisplayName}\"";
        requestConfiguration.QueryParameters.Count = true;
        requestConfiguration.QueryParameters.Top = 1;
        requestConfiguration.QueryParameters.Orderby = new string []{ "displayName" };
        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
    });

    Application? app;
    string? appId;
    string? objectId;
    if (result?.Value == null || result.Value.Count() == 0)
    {
        app = await CreateServicePrincipal(graphClient, appAccessConfig);
        appId = app?.AppId!;
        objectId = app?.Id!;
        Console.WriteLine($"Created {appAccessConfig.AppDisplayName} with AppId {appId} and ObjectId {objectId}");
    }
    else
    {
        appId = result?.Value?.First().AppId;
        objectId = result?.Value?.First().Id;
        Console.WriteLine($"Found {appAccessConfig.AppDisplayName} with AppId {appId} and ObjectId {objectId}");
    }
}

static async Task<Application?> CreateServicePrincipal(GraphServiceClient graphClient, ApplicationAccessConfig appAccessConfig)
{
    var requestBody = new Application
    {
        DisplayName = appAccessConfig.AppDisplayName
    };

    return await graphClient.Applications.PostAsync(requestBody);
}