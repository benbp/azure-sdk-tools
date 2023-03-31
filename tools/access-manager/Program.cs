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
            Console.WriteLine("Received error from Graph API:");
            Console.WriteLine("    Code:" + ex.Error?.Code);
            Console.WriteLine("    Message:" + ex.Error?.Message);
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
    var graphClient = new GraphServiceClient(new DefaultAzureCredential());
    var app = await FindOrCreateApplication(graphClient, appAccessConfig);
    if (app == null)
    {
        throw new Exception("Failed to find or create app, no error returned");
    }
    await SyncFederatedIdentityCredentials(graphClient, appAccessConfig, app);
}

static async Task SyncFederatedIdentityCredentials(
    GraphServiceClient graphClient,
    ApplicationAccessConfig appAccessConfig,
    Application app)
{
    Console.WriteLine("Syncing federated identity credentials for " + app.DisplayName);

    Func<FederatedIdentityCredential, FederatedIdentityCredentialsConfig, bool> federatedIdentityCredentialMatch = (cred, cfg) =>
    {
        return cred.Name == cfg.Name &&
               cred.Description == cfg.Description &&
               cred.Issuer == cfg.Issuer &&
               cred.Subject == cfg.Subject &&
               cred.Audiences == cfg.Audiences;
    };

    Console.WriteLine($"Found {app.FederatedIdentityCredentials?.Count()} federated identity credentials");
    app.FederatedIdentityCredentials?.Select(cred =>
    {
        return new FederatedIdentityCredentialsConfig
        {
            Name = cred.Name,
            Description = cred.Description,
            Issuer = cred.Issuer,
            Subject = cred.Subject,
            Audiences = cred.Audiences
        };
    }).ToString();

    int unchanged = 0, removed = 0, created = 0;

    // Remove any federated identity credentials that do not match the config
    foreach (var cred in app?.FederatedIdentityCredentials ?? Enumerable.Empty<FederatedIdentityCredential>())
    {
        var match = appAccessConfig.FederatedIdentityCredentials?.First((config) => federatedIdentityCredentialMatch(cred, config));
        if (match == null)
        {
            Console.WriteLine($"Removing federated identity credential {cred.Name}...");
            await graphClient.Applications[app?.Id].FederatedIdentityCredentials[cred.Id].DeleteAsync();
            Console.WriteLine($"Removed federated identity credential {cred.Name}");
            removed++;
        }
        else
        {
            unchanged++;
        }
    }

    // Create any federated identity credentials that are in the config without a match in the registered application
    foreach (var config in appAccessConfig.FederatedIdentityCredentials)
    {
        var match = app?.FederatedIdentityCredentials?.First((cred) => federatedIdentityCredentialMatch(cred, config));
        if (match == null)
        {
            var requestBody = config.ToFederatedIdentityCredential();

            Console.WriteLine($"Creating federated identity credential {config.Name}...");
            var newCred = await graphClient.Applications[app?.Id].FederatedIdentityCredentials.PostAsync(requestBody);
            Console.WriteLine($"Created federated identity credential {config.Name}...");
            created++;
        }
    }

    Console.WriteLine($"Updated federated identity credentials for app {app?.DisplayName} - {unchanged} unchanged, {removed} removed, {created} created");
}

static async Task<Application?> FindOrCreateApplication(GraphServiceClient graphClient, ApplicationAccessConfig appAccessConfig)
{
    Console.WriteLine($"Looking for app with display name {appAccessConfig.AppDisplayName}...");

    var result = await graphClient.Applications.GetAsync((requestConfiguration) =>
    {
        requestConfiguration.QueryParameters.Search = $"\"displayName:{appAccessConfig.AppDisplayName}\"";
        requestConfiguration.QueryParameters.Count = true;
        requestConfiguration.QueryParameters.Top = 1;
        requestConfiguration.QueryParameters.Orderby = new string []{ "displayName" };
        requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
    });

    if (result?.Value?.First() != null)
    {
        var foundApp = result.Value.First();
        Console.WriteLine($"Found {appAccessConfig.AppDisplayName} with AppId {foundApp.AppId} and ObjectId {foundApp.Id}");
        return foundApp;
    }

    Console.WriteLine($"App with display name {appAccessConfig.AppDisplayName} not found. Creating new app...");
    var requestBody = new Application
    {
        DisplayName = appAccessConfig.AppDisplayName
    };
    var app = await graphClient.Applications.PostAsync(requestBody);
    if (app == null)
    {
        return null;
    }
    Console.WriteLine($"Created {appAccessConfig.AppDisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
    return app;
}