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
    var app = await SyncApplication(graphClient, appAccessConfig);
    await SyncFederatedIdentityCredentials(graphClient, appAccessConfig, app);
}

static async Task SyncFederatedIdentityCredentials(
    GraphServiceClient graphClient,
    ApplicationAccessConfig appAccessConfig,
    Application? app)
{
    Func<FederatedIdentityCredential, FederatedIdentityCredentialsConfig, bool> federatedIdentityCredentialMatch = (cred, cfg) =>
    {
        return cred.Name == cfg.Name &&
               cred.Description == cfg.Description &&
               cred.Issuer == cfg.Issuer &&
               cred.Subject == cfg.Subject &&
               cred.Audiences == cfg.Audiences;
    };

    // Remove any federated identity credentials that do not match the config
    foreach (var cred in app?.FederatedIdentityCredentials!)
    {
        var match = appAccessConfig.FederatedIdentityCredentials?.First((cfg) => federatedIdentityCredentialMatch(cred, cfg));
        if (match == null)
        {
            await graphClient.Applications[app.AppId].FederatedIdentityCredentials[cred.Id].DeleteAsync();
        }
    }

    // Create any federated identity credentials that are in the config without a match in the registered application
    foreach (var cred in appAccessConfig.FederatedIdentityCredentials!)
    {
        var match = app.FederatedIdentityCredentials?.First((cfg) => federatedIdentityCredentialMatch(cfg, cred));
        if (match == null)
        {
            var requestBody = cred.ToFederatedIdentityCredential();

            var newCred = await graphClient.Applications[app.AppId].FederatedIdentityCredentials.PostAsync(requestBody);
            cred.Id = newCred?.Id;
        }
    }
}

static async Task<Application?> SyncApplication(GraphServiceClient graphClient, ApplicationAccessConfig appAccessConfig)
{

    var result = await graphClient.Applications.GetAsync((requestConfiguration) =>
    {
        requestConfiguration.QueryParameters.Search = $"\"displayName:{appAccessConfig.AppDisplayName}\"";
        requestConfiguration.QueryParameters.Count = true;
        requestConfiguration.QueryParameters.Top = 1;
        requestConfiguration.QueryParameters.Orderby = new string []{ "displayName" };
        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
    });

    Application? app = null;
    string? appId;
    string? objectId;
    if (result?.Value == null || result.Value.Count() == 0)
    {
        var requestBody = new Application
        {
            DisplayName = appAccessConfig.AppDisplayName
        };
        app = await graphClient.Applications.PostAsync(requestBody);
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

    return app;
}