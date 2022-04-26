using System;
using Octokit;
using Microsoft.Extensions.Logging;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;

namespace Azure.Sdk.Tools.CheckEnforcer.Integrations.GitHub
{
    public static class OctokitGitHubClientFactory
    {
        public static GitHubClient GetGitHubClient(IGlobalConfigurationProvider config, ProductHeaderValue productHeaderValue, Credentials credentials, ILogger log)
        {
            var mode = config.GetApplicationMode();

            if (mode == "local")
            {
                var connection = new GitHubLocalConnection(log);
                return new GitHubClient(connection);
            }

            return new GitHubClient(productHeaderValue)
            {
                Credentials = credentials
            };
        }
    }
}
