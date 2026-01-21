// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Nodes;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class PackageInfoArtifactFilter
{
    public static List<JsonObject> FilterByArtifacts(
        List<JsonObject> packages,
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
            if (!pkg.TryGetPropertyValue("ArtifactName", out var artifactNode) || string.IsNullOrEmpty(artifactNode?.ToString()))
            {
                var packageName = pkg["Name"]?.ToString() ?? "(unknown)";
                warn($"Package '{packageName}' does not have an 'ArtifactName' property and will be excluded from artifact filtering.");
            }
        }

        var filtered = packages
            .Where(pkg =>
            {
                var artifactName = pkg["ArtifactName"]?.ToString();
                return !string.IsNullOrEmpty(artifactName) && artifactSet.Contains(artifactName);
            })
            .ToList();

        if (filtered.Count == 0)
        {
            throw new InvalidOperationException("No packages found matching the provided artifact list");
        }

        return filtered;
    }
}
