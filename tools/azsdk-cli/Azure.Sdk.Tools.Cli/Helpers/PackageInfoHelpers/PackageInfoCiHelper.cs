// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;

/// <summary>
/// Helper for extracting CI parameters and triggering paths from ci*.yml files.
/// </summary>
internal static class PackageInfoCiHelper
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Populates CI parameters and triggering paths on a PackageInfo instance.
    /// </summary>
    public static void PopulateCiParameters(PackageInfo info)
    {
        if (info.Language != SdkLanguage.DotNet)
        {
            return;
        }

        var ciYamlResult = TryFindCiYaml(info);
        if (ciYamlResult == null)
        {
            info.CiParameters = new CiParameters
            {
                BuildSnippets = true,
                CheckAotCompat = info.AotCompatOptOut == false,
                AotTestInputs = []
            };
            return;
        }

        var (ciYaml, ciYamlPath) = ciYamlResult.Value;
        var parameters = ciYaml.Extends?.Parameters;
        var repoRoot = info.RepoRoot;
        var ciYamlDir = Path.GetDirectoryName(ciYamlPath) ?? string.Empty;

        // Find the artifact entry for this package
        var artifact = parameters?.Artifacts?
            .FirstOrDefault(a => string.Equals(a.Name, info.ArtifactName, StringComparison.OrdinalIgnoreCase));

        // Extract AOT test inputs for this artifact
        var aotInputs = parameters?.AotTestInputs?
            .Where(a => string.IsNullOrWhiteSpace(info.ArtifactName) ||
                        string.Equals(a.ArtifactName, info.ArtifactName, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

        var hasBaselinedWarnings = aotInputs.Any(a => a.HasWarningsFile);

        var buildSnippets = parameters?.BuildSnippets ?? true;
        var checkAotCompat = parameters?.CheckAotCompat ?? (hasBaselinedWarnings || info.AotCompatOptOut != true);

        // Collect matrix configs
        var matrixConfigs = new List<Dictionary<string, object?>>();
        AddMatrixConfigs(matrixConfigs, parameters?.MatrixConfigs);
        AddMatrixConfigs(matrixConfigs, parameters?.AdditionalMatrixConfigs);

        // Collect triggering paths
        var triggeringPaths = new List<string>();
        if (artifact?.TriggeringPaths != null)
        {
            triggeringPaths.AddRange(artifact.TriggeringPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
        if (parameters?.TriggeringPaths != null)
        {
            triggeringPaths.AddRange(parameters.TriggeringPaths.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        var ciYamlRelative = GetRelativePath(ciYamlPath, repoRoot);
        if (!string.IsNullOrEmpty(ciYamlRelative))
        {
            triggeringPaths.Add("/" + ciYamlRelative);
        }

        var resolvedTriggers = ResolveTriggeringPaths(triggeringPaths, ciYamlDir, repoRoot);

        // Additional validation packages
        var additionalPackages = artifact?.AdditionalValidationPackages?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (NormalizedPath)p)
            .ToList() ?? [];

        // Update PackageInfo
        info.TriggeringPaths = resolvedTriggers;
        info.AdditionalValidationPackages = additionalPackages.Count > 0 ? additionalPackages : null;
        info.CiParameters = new CiParameters
        {
            BuildSnippets = buildSnippets,
            CheckAotCompat = checkAotCompat,
            AotTestInputs = checkAotCompat ? ConvertAotInputs(aotInputs) : [],
            MatrixConfigs = matrixConfigs
        };
    }

    private static (CiPipelineYaml Yaml, string Path)? TryFindCiYaml(PackageInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.ServiceDirectory))
        {
            return null;
        }

        var repoRoot = info.RepoRoot;
        var sdkCiDir = Path.Combine(repoRoot, "sdk", info.ServiceDirectory);
        var engCiDir = Path.Combine(repoRoot, "eng", info.ServiceDirectory);

        var ciRoot = Directory.Exists(sdkCiDir) ? sdkCiDir : engCiDir;
        if (!Directory.Exists(ciRoot))
        {
            return null;
        }

        var ciFiles = Directory.GetFiles(ciRoot, "ci*.yml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ciFiles.Length == 0)
        {
            return null;
        }

        var isSoleCiYaml = ciFiles.Length == 1;

        foreach (var ciFile in ciFiles)
        {
            var yaml = DeserializeYaml<CiPipelineYaml>(ciFile);
            if (yaml == null)
            {
                continue;
            }

            if (isSoleCiYaml || MatchesArtifact(yaml, info.ArtifactName, info.Group))
            {
                return (yaml, ciFile);
            }
        }

        return null;
    }

    private static T? DeserializeYaml<T>(string path) where T : class
    {
        try
        {
            using var reader = new StreamReader(path);
            return YamlDeserializer.Deserialize<T>(reader);
        }
        catch
        {
            return null;
        }
    }

    private static bool MatchesArtifact(CiPipelineYaml yaml, string? artifactName, string? group)
    {
        var artifacts = yaml.Extends?.Parameters?.Artifacts;
        if (artifacts == null)
        {
            return false;
        }

        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Name) ||
                !string.Equals(artifact.Name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(group))
            {
                return true;
            }

            if (string.Equals(artifact.GroupId, group, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<Dictionary<string, object?>> ConvertAotInputs(List<CiPipelineYamlAotTestInput> inputs)
    {
        var result = new List<Dictionary<string, object?>>();

        foreach (var input in inputs)
        {
            var dict = new Dictionary<string, object?>
            {
                ["ArtifactName"] = input.ArtifactName
            };

            // Use the original property name from the YAML
            if (!string.IsNullOrWhiteSpace(input.ExpectedWarningsFilePath))
            {
                dict["ExpectedWarningsFilePath"] = input.ExpectedWarningsFilePath;
            }
            else if (!string.IsNullOrWhiteSpace(input.ExpectedWarningsFilepathAlt))
            {
                dict["ExpectedWarningsFilepath"] = input.ExpectedWarningsFilepathAlt;
            }

            result.Add(dict);
        }

        return result;
    }

    private static void AddMatrixConfigs(List<Dictionary<string, object?>> destination, List<Dictionary<string, object>>? source)
    {
        if (source == null)
        {
            return;
        }

        foreach (var config in source)
        {
            // Convert Dictionary<string, object> to Dictionary<string, object?>
            var converted = config.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
            destination.Add(converted);
        }
    }

    private static List<NormalizedPath> ResolveTriggeringPaths(List<string> paths, string ciYamlDir, string repoRoot)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var triggerPath in paths)
        {
            if (string.IsNullOrWhiteSpace(triggerPath))
            {
                continue;
            }

            string fullPath;
            if (triggerPath.StartsWith("/"))
            {
                fullPath = Path.Combine(repoRoot, triggerPath.TrimStart('/'));
            }
            else
            {
                fullPath = Path.Combine(ciYamlDir, triggerPath);
            }

            try
            {
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    fullPath = Path.GetFullPath(fullPath);
                }

                var relative = GetRelativePath(fullPath, repoRoot);
                if (!string.IsNullOrEmpty(relative))
                {
                    resolved.Add("/" + relative);
                }
            }
            catch
            {
                // Skip failed path resolution
            }
        }

        return resolved.Select(p => (NormalizedPath)p).ToList();
    }

    private static string GetRelativePath(string fullPath, string repoRoot)
    {
        try
        {
            return Path.GetRelativePath(repoRoot, fullPath).Replace("\\", "/");
        }
        catch
        {
            return string.Empty;
        }
    }
}
