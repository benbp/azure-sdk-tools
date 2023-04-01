using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

public class Reconciler
{
    public GraphClient GraphClient { get; set; }

    public Reconciler(GraphClient graphClient)
    {
        GraphClient = graphClient;
    }

    public async Task Reconcile(AccessConfig accessConfig)
    {
        try
        {
            foreach (var cfg in accessConfig.ApplicationAccessConfigs ?? Enumerable.Empty<ApplicationAccessConfig>())
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

        var credentials = await GraphClient.ListFederatedIdentityCredentials(app);

        int unchanged = 0, removed = 0, created = 0;

        // Remove any federated identity credentials that do not match the config
        foreach (var cred in credentials ?? Enumerable.Empty<FederatedIdentityCredential>())
        {
            var match = appAccessConfig.FederatedIdentityCredentials?.FirstOrDefault(config => config == cred);
            if (match is null)
            {
                await GraphClient.DeleteFederatedIdentityCredential(app, cred);
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
            var match = credentials?.FirstOrDefault(cred => config == cred);
            if (match is null)
            {
                await GraphClient.CreateFederatedIdentityCredential(app, config);
                created++;
            }
        }

        Console.WriteLine($"Updated federated identity credentials for app {app?.DisplayName} - {unchanged} unchanged, {removed} removed, {created} created");
    }

    public async Task<Application?> ReconcileApplication(ApplicationAccessConfig appAccessConfig)
    {
        Console.WriteLine($"Looking for app with display name {appAccessConfig.AppDisplayName}...");

        var app = await GraphClient.GetApplicationByDisplayName(appAccessConfig.AppDisplayName);

        if (app is not null)
        {
            Console.WriteLine($"Found {app.DisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
            return app;
        }

        Console.WriteLine($"App with display name {appAccessConfig.AppDisplayName} not found. Creating new app...");
        var requestBody = new Application
        {
            DisplayName = appAccessConfig.AppDisplayName
        };
        app = await GraphClient.CreateApplication(requestBody);
        Console.WriteLine($"Created {appAccessConfig.AppDisplayName} with AppId {app.AppId} and ObjectId {app.Id}");
        return app;
    }
}