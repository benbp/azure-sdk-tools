using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.MachineLearning;
using Azure.ResourceManager.Resources;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentServiceFactory
{
    IAzureAgentService Create(string? model = null, string? projectEndpoint = null);
    Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string? accountName = null, string? projectName = null, CancellationToken? ct = null);
}

public class AzureAgentServiceFactory(IAzureService azureService, ILogger<AzureAgentService> logger) : IAzureAgentServiceFactory
{
    public IAzureAgentService Create(string? model = null, string? projectEndpoint = null)
    {
        return new AzureAgentService(azureService, logger, projectEndpoint, model);
    }

    public async Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string? accountName = null, string? projectName = null, CancellationToken? ct = null)
    {
        string projectEndpoint;
        var credential = azureService.GetCredential();
        var armClient = new ArmClient(credential, subscriptionId);
        var group = await armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroup)).GetAsync(ct ?? CancellationToken.None);

        if (!string.IsNullOrEmpty(accountName) && !string.IsNullOrEmpty(projectName))
        {
            projectEndpoint = $"https://{accountName}.services.ai.azure.com/api/projects/{projectName}";
            logger.LogDebug("Using project endpoint {ProjectEndpoint} for azure agent client", projectEndpoint);
            return new AzureAgentService(azureService, logger, projectEndpoint, null);
        }

        var accounts = group.Value.GetCognitiveServicesAccounts().ToList();
        var workspaces = group.Value.GetMachineLearningWorkspaces().ToList();
        if (workspaces.Count > 1 || accounts.Count > 1)
        {
            logger.LogError("{accountCount} cognitive service accounts and {workspaceCount} machine learning workspaces found in resource group {ResourceGroup}. Specify account name and project name.",
                                accounts.Count, workspaces.Count, resourceGroup);
            logger.LogDebug("Available accounts: {Accounts}", string.Join(", ", accounts.Select(a => a.Data.Name)));
            logger.LogDebug("Available workspaces: {Workspaces}", string.Join(", ", workspaces.Select(w => w.Data.Name)));
            return null;
        }

        if (accounts.Count == 0 || workspaces.Count == 0)
        {
            logger.LogError("{accountCount} cognitive service accounts and {workspaceCount} machine learning workspaces found in resource group {ResourceGroup}.",
                                accounts.Count, workspaces.Count, resourceGroup);
            return null;
        }

        projectEndpoint = $"https://{accounts[0].Data.Name}.services.ai.azure.com/api/projects/{workspaces[0].Data.Name}";
        logger.LogDebug("Using project endpoint {ProjectEndpoint} for azure agent client", projectEndpoint);
        return new AzureAgentService(azureService, logger, projectEndpoint, null);
    }
}
