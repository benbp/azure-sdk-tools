using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

public class Reconciler
{
    public GraphServiceClient GraphClient { get; set; }

    public AccessConfig AccessConfig { get; set; }

    public Reconciler(GraphServiceClient graphServiceClient, AccessConfig accessConfig)
    {
        GraphClient = graphServiceClient;
        AccessConfig = accessConfig;
    }

    public async Task Reconcile()
    {
        try
        {
            foreach (var cfg in AccessConfig.ApplicationAccessConfigs ?? Enumerable.Empty<ApplicationAccessConfig>())
            {
                var app = await ReconcileApplication(cfg);
                if (app is null)
                {
                    throw new Exception("Failed to find or create app, no error returned");
                }
                await ReconcileFederatedIdentityCredentials(cfg, app);
                // TODO: Add RBAC sync
            }
        }
        catch (ODataError ex)
        {
            Console.WriteLine("Received error from Graph API:");
            Console.WriteLine("    Code:" + ex.Error?.Code);
            Console.WriteLine("    Message:" + ex.Error?.Message);
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Environment.Exit(2);
        }
    }

    public async Task ReconcileFederatedIdentityCredentials(ApplicationAccessConfig appAccessConfig, Application app)
    {
        Console.WriteLine("Syncing federated identity credentials for " + app.DisplayName);

        var result = await GraphClient.Applications[app?.Id].FederatedIdentityCredentials.GetAsync();
        var fic = result?.Value;

        Console.WriteLine($"Found {fic?.Count() ?? 0} federated identity credentials ->");
        fic?.ForEach(cred => Console.WriteLine(((FederatedIdentityCredentialsConfig)cred).ToIndentedString(1)));

        int unchanged = 0, removed = 0, created = 0;

        // Remove any federated identity credentials that do not match the config
        foreach (var cred in fic ?? Enumerable.Empty<FederatedIdentityCredential>())
        {
            var match = appAccessConfig.FederatedIdentityCredentials?.FirstOrDefault(config => config == cred);
            if (match is null)
            {
                Console.WriteLine($"Removing federated identity credential {cred.Name}...");
                await GraphClient.Applications[app?.Id].FederatedIdentityCredentials[cred.Id].DeleteAsync();
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
            var match = fic?.FirstOrDefault(cred => config == cred);
            if (match is null)
            {
                Console.WriteLine($"Creating federated identity credential {config.Name}...");
                var newCred = await GraphClient.Applications[app?.Id].FederatedIdentityCredentials.PostAsync(config);
                Console.WriteLine($"Created federated identity credential {config.Name}...");
                created++;
            }
        }

        Console.WriteLine($"Updated federated identity credentials for app {app?.DisplayName} - {unchanged} unchanged, {removed} removed, {created} created");
    }

    public async Task<Application?> ReconcileApplication(ApplicationAccessConfig appAccessConfig)
    {
        Console.WriteLine($"Looking for app with display name {appAccessConfig.AppDisplayName}...");

        var result = await GraphClient.Applications.GetAsync((requestConfiguration) =>
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
        var app = await GraphClient.Applications.PostAsync(requestBody);
        if (app is null)
        {
            return null;
        }
        Console.WriteLine($"Created {appAccessConfig.AppDisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        return app;
    }
}