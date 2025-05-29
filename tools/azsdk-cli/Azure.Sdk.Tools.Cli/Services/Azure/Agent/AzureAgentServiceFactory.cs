using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.CognitiveServices;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IAzureAgentServiceFactory
{
    IAzureAgentService Create(string? model = null, string? connectionString = null);
    Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string projectName, CancellationToken ct);
}

public class AzureAgentServiceFactory(IAzureService azureService, ILogger<AzureAgentService> logger) : IAzureAgentServiceFactory
{
    public IAzureAgentService Create(string? model = null, string? connectionString = null)
    {
        return new AzureAgentService(azureService, logger, connectionString, model);
    }

    public async Task<IAzureAgentService> Create(string subscriptionId, string resourceGroup, string projectName, CancellationToken ct)
    {
        var credential = azureService.GetCredential();
        var armClient = new ArmClient(credential, subscriptionId);
        var group = await armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroup)).GetAsync(ct);
        var cognitiveServicesAccounts = group.Value.GetCognitiveServicesAccounts();
        logger.LogInformation("Found {Count} cognitive services accounts in resource group {ResourceGroup}", cognitiveServicesAccounts.Count(), resourceGroup);
        logger.LogInformation("Account names: {AccountNames}", string.Join(", ", cognitiveServicesAccounts.Select(a => a.Data.Name)));
        var account = cognitiveServicesAccounts.FirstOrDefault(a => a.Data.Name == projectName);
        if (account == null)
        {
            return null;
        }

        var connectionString = $"{account.Data.Location}.api.azureml.ms;{subscriptionId};{projectName};{projectName}";
        return new AzureAgentService(azureService, logger, connectionString, null);
    }

}