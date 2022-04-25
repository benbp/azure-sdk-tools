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

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub
{
    public static class OctokitGitHubClientFactory
    {
        public static GitHubClient GetGitHubClient(IGlobalConfigurationProvider config, ProductHeaderValue productHeaderValue, Credentials credentials)
        {
            var mode = config.GetApplicationMode();

            if (mode == "local")
            {
                var connection = new GitHubLocalConnection();
                return new GitHubClient(connection);
            }

            return new GitHubClient(productHeaderValue)
            {
                Credentials = credentials
            };
        }
    }
}
