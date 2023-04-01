using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

/*
 * Wrapper for Microsoft.Graph.GraphServiceClient
 */
public class GraphClient : IGraphClient
{
    public GraphServiceClient GraphServiceClient { get; }

    public GraphClient()
    {
        GraphServiceClient = new GraphServiceClient(new DefaultAzureCredential());
    }

    public async Task<Application?> GetApplicationByDisplayName(string displayName)
    {
        var result = await GraphServiceClient.Applications.GetAsync((requestConfiguration) =>
        {
            requestConfiguration.QueryParameters.Search = $"\"displayName:{displayName}\"";
            requestConfiguration.QueryParameters.Count = true;
            requestConfiguration.QueryParameters.Top = 1;
            requestConfiguration.QueryParameters.Orderby = new string []{ "displayName" };
            requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
        });

        return result?.Value?.First();
    }

    public async Task<Application> CreateApplication(Application application)
    {
        var app = await GraphServiceClient.Applications.PostAsync(application);
        if (app is null)
        {
            throw new Exception($"Failed to create app with display name {application.DisplayName}, Graph API returned empty response.");
        }
        return app;
    }

    public async Task<List<FederatedIdentityCredential>> ListFederatedIdentityCredentials(Application app)
    {
        Console.WriteLine($"Listing federated identity credentials for app {app.AppId}...");
        var result = await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials.GetAsync();

        var credentials = result?.Value;

        Console.WriteLine($"Found {credentials?.Count() ?? 0} federated identity credentials ->");
        return credentials ?? new List<FederatedIdentityCredential>();
    }

    public async Task<FederatedIdentityCredential> CreateFederatedIdentityCredential(Application app, FederatedIdentityCredential credential)
    {
        Console.WriteLine($"Creating federated identity credential {credential.Name} for app {app.AppId}...");
        var created = await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials.PostAsync(credential);
        if (created is null)
        {
            throw new Exception($"Failed to create federated identity credential {credential.Name} for app {app.AppId}, Graph API returned empty response.");
        }
        Console.WriteLine($"Created federated identity credential {created.Name} for app {app.AppId}...");
        return created;
    }

    public async Task DeleteFederatedIdentityCredential(Application app, FederatedIdentityCredential credential)
    {
        Console.WriteLine($"Deleting federated identity credential {credential.Name} for app {app.AppId}...");
        await GraphServiceClient.Applications[app.Id].FederatedIdentityCredentials[credential.Id].DeleteAsync();
        Console.WriteLine($"Deleted federated identity credential {credential.Name} for app {app.AppId}...");
    }
}

public interface IGraphClient
{
    public Task<Application?> GetApplicationByDisplayName(string displayName);
    public Task<Application> CreateApplication(Application application);
    public Task<List<FederatedIdentityCredential>> ListFederatedIdentityCredentials(Application app);
    public Task<FederatedIdentityCredential> CreateFederatedIdentityCredential(Application app, FederatedIdentityCredential credential);
    public Task DeleteFederatedIdentityCredential(Application app, FederatedIdentityCredential credential);
}