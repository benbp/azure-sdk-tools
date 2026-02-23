// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;

public static class PackageInfoArtifactFilter
{
    public static List<PackageInfo> FilterPackagesByArtifact(List<PackageInfo> packages, string[] artifactList, ILogger? logger = null)
    {
        if (artifactList is null)
        {
            return packages;
        }

        var artifactArray = artifactList ?? artifactList.ToArray();
        if (artifactArray.Length == 0)
        {
            return packages;
        }

        var filteredArtifacts = artifactArray
            .Where(artifact => !string.IsNullOrWhiteSpace(artifact))
            .Select(artifact => artifact.Trim())
            .ToArray();

        if (filteredArtifacts.Length == 0)
        {
            logger?.LogWarning("Artifact list contains no valid entries");
            return packages;
        }

        var artifactSet = new HashSet<string>(filteredArtifacts, StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages)
        {
            if (string.IsNullOrEmpty(pkg.ArtifactName))
            {
                logger?.LogWarning(
                    "Package '{PackageName}' does not have an 'ArtifactName' property and will be excluded from artifact filtering.",
                    pkg.PackageName ?? "(unknown)");
            }
        }

        var filtered = packages
            .Where(pkg => !string.IsNullOrEmpty(pkg.ArtifactName) && artifactSet.Contains(pkg.ArtifactName))
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException("No packages found matching the provided artifact list");
        }

        return filtered;
    }
}
