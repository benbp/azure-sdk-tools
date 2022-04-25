using Azure.Core;
using Azure.Identity;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Octokit;
using System;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GithubLocalClient : IGitHubClientProvider
    {
        public GithubLocalClient(IGlobalConfigurationProvider globalConfigurationProvider, IMemoryCache cache, CryptographyClient cryptographyClient, GitHubRateLimiter limiter)
        {
            this.globalConfigurationProvider = globalConfigurationProvider;
            this.cache = cache;
            this.cryptographyClient = cryptographyClient;
            this.limiter = limiter;
        }

        private IGlobalConfigurationProvider globalConfigurationProvider;
        private IMemoryCache cache;
        private CryptographyClient cryptographyClient;
        private GitHubRateLimiter limiter;


        public async Task<GitHubClient> GetApplicationClientAsync(CancellationToken cancellationToken)
        {
            var token = "foo";

            var appClient = new GitHubClient(new ProductHeaderValue(globalConfigurationProvider.GetApplicationName()))
            {
                Credentials = new Credentials(token, AuthenticationType.Bearer)
            };

            return await Task.FromResult<Octokit.GitHubClient>(appClient);
        }

        private async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
        {
            return await Task.FromResult<string>("foo");
        }


        public async Task<GitHubClient> GetInstallationClientAsync(long installationId, CancellationToken cancellationToken)
        {
            var installationToken = await GetInstallationTokenAsync(installationId, cancellationToken);
            var installationClient = new GitHubClient(new ProductHeaderValue($"{globalConfigurationProvider.GetApplicationName()}-{installationId}"))
            {
                Credentials = new Credentials(installationToken)
            };

            return installationClient;
        }
    }
}