// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;

public static class PackageInfoArtifactFilter
{
    public static List<PackageInfo> FilterByArtifacts(
        List<PackageInfo> packages,
        IEnumerable<string>? artifactList,
        Action<string> warn)
    {
        if (artifactList is null)
        {
            return packages;
        }

        var artifactArray = artifactList as string[] ?? artifactList.ToArray();
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
            warn("Artifact list contains no valid entries");
            return packages;
        }

        var artifactSet = new HashSet<string>(filteredArtifacts, StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages)
        {
            if (string.IsNullOrEmpty(pkg.ArtifactName))
            {
                warn($"Package '{pkg.PackageName ?? "(unknown)"}' does not have an 'ArtifactName' property and will be excluded from artifact filtering.");
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
