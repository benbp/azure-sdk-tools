// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.IO;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IGitHelper
    {
        public string DiscoverRepoRoot(string pathInRepo, CancellationToken ct);
        public Task<string> GetRepoOwnerName(string pathInRepo, bool findUpstreamParent, CancellationToken ct);
        public Task<string> GetRepoFullName(string pathInRepo, bool findUpstreamParent, CancellationToken ct);
        public Task<Uri> GetRepoRemoteUri(string pathInRepo, CancellationToken ct);
        public Task<string> GetBranchName(string pathInRepo, CancellationToken ct);
        public Task<string> GetMergeBaseCommitSha(string pathInRepo, string targetBranch, CancellationToken ct);
        public Task<string> GetRepoName(string pathInRepo, CancellationToken ct);
    }

    public class GitHelper(IGitHubService gitHubService, ILogger<GitHelper> logger, IProcessHelper processHelper) : IGitHelper
    {
        /// <summary>
        /// Gets the SHA of the merge base (common ancestor) between the current branch and the target branch.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="targetBranchName">The name of the target branch to find the merge base with</param>
        /// <returns>The SHA of the merge base commit, or empty string if not found</returns>
        public async Task<string> GetMergeBaseCommitSha(string pathInRepo, string targetBranchName, CancellationToken ct)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo, ct);
            var result = await processHelper.Run(new("git", ["merge-base", "HEAD", targetBranchName], workingDirectory: repoRoot), ct);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to get merge base commit SHA: {result.Output}");
            }
            return result.Output;
        }

        /// <summary>
        /// Gets the friendly name of the current branch in the repository.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The friendly name of the current branch</returns>
        public async Task<string> GetBranchName(string pathInRepo, CancellationToken ct)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo, ct);
            var result = await processHelper.Run(new("git", ["rev-parse", "--abbrev-ref", "HEAD"], workingDirectory: repoRoot), ct);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to get current branch name: {result.Output}");
            }
            return result.Output;
        }

        /// <summary>
        /// Gets the remote origin URI of the repository in HTTPS format.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The HTTPS URI of the remote origin</returns>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine remote URL</exception>
        public async Task<Uri> GetRepoRemoteUri(string pathInRepo, CancellationToken ct)
        {
            var repoRoot = DiscoverRepoRoot(pathInRepo, ct);
            var result = await processHelper.Run(new("git", ["config", "--get", "remote.origin.url"], workingDirectory: repoRoot), ct);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to get remote origin URL: {result.Output}");
            }

            var remoteUrl = result.Output?.Trim();
            if (string.IsNullOrEmpty(remoteUrl))
            {
                throw new InvalidOperationException("Unable to determine remote URL.");
            }

            var url = ConvertSshToHttpsUrl(remoteUrl);
            return new Uri(url);
        }

        /// <summary>
        /// Converts SSH GitHub URLs to HTTPS format
        /// </summary>
        /// <param name="gitUrl">The Git URL (SSH or HTTPS)</param>
        /// <returns>HTTPS formatted Git URL</returns>
        private static string ConvertSshToHttpsUrl(string gitUrl)
        {
            if (string.IsNullOrEmpty(gitUrl))
            {
                return gitUrl;
            }

            // If it's already HTTPS, return as-is
            if (gitUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return gitUrl;
            }

            // Handle GitHub SSH URLs (e.g., git@github.com:Azure/azure-rest-api-specs.git)
            if (gitUrl.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                // Convert SSH URL to HTTPS URL
                // git@github.com:Azure/azure-rest-api-specs.git -> https://github.com/Azure/azure-rest-api-specs.git
                return gitUrl.Replace("git@github.com:", "https://github.com/");
            }

            // Return as-is if it's not a recognized format
            return gitUrl;
        }

        /// <summary>
        /// Gets the owner name of the repository, optionally finding the upstream parent if the repo is a fork.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="findUpstreamParent">Whether to find the upstream parent repo if this is a fork (default: true)</param>
        /// <returns>The owner name of the repository or its upstream parent</returns>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository owner</exception>
        public async Task<string> GetRepoOwnerName(string pathInRepo, bool findUpstreamParent = true, CancellationToken ct = default)
        {
            var uri = await GetRepoRemoteUri(pathInRepo, ct);
            var segments = uri.Segments;
            string repoOwner = string.Empty;
            string repoName = string.Empty;
            if (segments.Length > 2)
            {
                repoOwner = segments[^2].TrimEnd('/');
                repoName = segments[^1].TrimEnd(".git".ToCharArray());
            }

            if (findUpstreamParent)
            {
                // Check if the repo is a fork and get the parent repo
                var parentRepoUrl = await gitHubService.GetGitHubParentRepoUrlAsync(repoOwner, repoName);
                logger.LogDebug($"Parent repo URL: {parentRepoUrl}");
                if (!string.IsNullOrEmpty(parentRepoUrl))
                {
                    var parentSegments = new Uri(parentRepoUrl).Segments;
                    if (parentSegments.Length > 2)
                    {
                        repoOwner = parentSegments[^2].TrimEnd('/');
                    }
                }
            }

            if (!string.IsNullOrEmpty(repoOwner))
            {
                return repoOwner;
            }

            throw new InvalidOperationException("Unable to determine repository owner.");
        }

        /// <summary>
        /// Gets the full name of the repository in the format "{owner}/{name}", e.g. "Azure/azure-rest-api-specs".
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <param name="findUpstreamParent">Whether to find the upstream parent repo if this is a fork (default: true)</param>
        /// <returns>The full name of the repository in "owner/name" format</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        public async Task<string> GetRepoFullName(string pathInRepo, bool findUpstreamParent, CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(pathInRepo))
            {
                var repoOwner = await GetRepoOwnerName(pathInRepo, findUpstreamParent, ct);
                var repoName = await GetRepoName(pathInRepo, ct);
                return $"{repoOwner}/{repoName}";
            }

            throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
        }

        /// <summary>
        /// Discovers and returns the root directory path of the git repository containing the specified path.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The absolute path to the repository root directory</returns>
        /// <exception cref="InvalidOperationException">Thrown when no git repository is found at or above the specified path</exception>
        public string DiscoverRepoRoot(string pathInRepo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathInRepo))
            {
                throw new ArgumentException("Invalid path", nameof(pathInRepo));
            }

            var fullPath = Path.GetFullPath(pathInRepo);
            var dir = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath).Directory;
            if (dir == null)
            {
                throw new InvalidOperationException($"Cannot determine directory from path: {pathInRepo}");
            }

            while (dir != null)
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new InvalidOperationException($"No git repository root found for path: {pathInRepo}");
        }

        /// <summary>
        /// Gets the repository name from the remote origin URL.
        /// </summary>
        /// <param name="pathInRepo">Any path within the git repository (file or directory)</param>
        /// <returns>The name of the repository (without the owner)</returns>
        /// <exception cref="ArgumentException">Thrown when pathInRepo is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown when unable to determine repository name from remote URL</exception>
        public async Task<string> GetRepoName(string pathInRepo, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(pathInRepo))
            {
                throw new ArgumentException("Invalid repository path.", nameof(pathInRepo));
            }

            var uri = await GetRepoRemoteUri(pathInRepo, ct);
            var segments = uri.Segments;

            if (segments.Length < 2)
            {
                throw new InvalidOperationException($"Unable to parse repository name from remote URL: {uri}");
            }

            string repoName = segments[^1].TrimEnd(".git".ToCharArray());
            return repoName;
        }
    }
}
