using Azure.ResourceManager;
using Azure.ResourceManager.MachineLearning;
using Azure.ResourceManager.Resources;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentServiceFactory
{
    IAzureAgentService Create(string? model = null, string? projectEndpoint = null);
    Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string projectName, CancellationToken ct);
}

public class AzureAgentServiceFactory(IAzureService azureService, ILogger<AzureAgentService> logger) : IAzureAgentServiceFactory
{
    public IAzureAgentService Create(string? model = null, string? projectEndpoint = null)
    {
        return new AzureAgentService(azureService, logger, projectEndpoint, model);
    }

    public async Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string projectName, CancellationToken ct)
    {
        var credential = azureService.GetCredential();
        var armClient = new ArmClient(credential, subscriptionId);
        var group = await armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroup)).GetAsync(ct);
        var machineLearningWorkspaces = group.Value.GetMachineLearningWorkspaces();
        logger.LogInformation("Found {Count} machine learning workspaces in resource group {ResourceGroup}", machineLearningWorkspaces.Count(), resourceGroup);
        var workspace = machineLearningWorkspaces.FirstOrDefault(w => w.Data.Name == projectName);
        if (workspace == null)
        {
            logger.LogWarning("No machine learning workspace found with name {ProjectName} in resource group {ResourceGroup}", projectName, resourceGroup);
            return null;
        }

        var projectEndpoint = $"{workspace.Data.Location}.api.azureml.ms;{subscriptionId};{projectName};{projectName}";
        return new AzureAgentService(azureService, logger, projectEndpoint, null);
    }

}
