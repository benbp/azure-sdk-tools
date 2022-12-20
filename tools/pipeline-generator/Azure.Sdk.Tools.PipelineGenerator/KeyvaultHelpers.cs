using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Core;
using Azure.ResourceManager.KeyVault;

namespace PipelineGenerator
{
    public class KeyvaultHelper
    {
        public bool WhatIf { get; }
        public string SubscriptionId { get; }
        public string ResourceGroup { get; }
        private KeyVaultManagementClient client { get; }

        public KeyvaultHelper(
            DefaultAzureCredential credential,
            string subscriptionId,
            string resourceGroup,
            bool whatIf
        )
        {
            this.WhatIf = whatIf;
            this.SubscriptionId = subscriptionId;
            this.ResourceGroup = resourceGroup;
            this.client = new KeyVaultManagementClient(this.SubscriptionId, credential);
        }

        public async Task CreateOrUpdateKeyvault(string name, string resourceGroup, CancellationToken cancellationToken)
        {
            var vaults = this.client.Vaults.ListByResourceGroupAsync(this.ResourceGroup, cancellationToken: cancellationToken);
            vaults.
        }
    }
}