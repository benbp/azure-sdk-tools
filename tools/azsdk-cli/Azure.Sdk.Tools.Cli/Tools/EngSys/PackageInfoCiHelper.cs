// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

/// <summary>
/// Helper for extracting CI parameters and triggering paths from ci*.yml files.
/// </summary>
internal static class PackageInfoCiHelper
{
    /// <summary>
    /// Populates CI parameters and triggering paths on a PackageInfo instance.
    /// Call this after basic package info is populated but before serialization.
    /// </summary>
    public static void PopulateCiParameters(PackageInfo info)
    {
        // Only .NET has CI parameter extraction implemented currently
        if (info.Language != SdkLanguage.DotNet)
        {
            return;
        }

        var ciYamlResult = TryFindCiYaml(info);
        if (ciYamlResult == null)
        {
            // No CI YAML found - use defaults based on project settings
            info.CiParameters = new CiParameters
            {
                BuildSnippets = true,
                CheckAotCompat = info.AotCompatOptOut == false,
                AotTestInputs = [],
                MatrixConfigs = []
            };
            return;
        }

        var (yaml, ciYamlPath) = ciYamlResult.Value;
        var repoRoot = info.RepoRoot;
        var ciYamlDir = Path.GetDirectoryName(ciYamlPath) ?? string.Empty;

        // Extract CI parameters
        var buildSnippets = GetBooleanParameter(yaml, "extends", "parameters", "BuildSnippets") ?? true;
        var aotInputs = GetAotTestInputs(yaml, info.ArtifactName);
        var hasBaselinedWarnings = aotInputs.Any(e => e.HasWarningsFile);
        
        var checkAotCompat = GetBooleanParameter(yaml, "extends", "parameters", "CheckAOTCompat")
            ?? (hasBaselinedWarnings || info.AotCompatOptOut != true);

        var matrixConfigs = new List<JsonObject>();
        AddMatrixConfigs(matrixConfigs, yaml, "extends", "parameters", "MatrixConfigs");
        AddMatrixConfigs(matrixConfigs, yaml, "extends", "parameters", "AdditionalMatrixConfigs");

        // Extract triggering paths
        var triggeringPaths = new List<string>();
        
        // 1. Get artifact-specific triggering paths
        triggeringPaths.AddRange(GetArtifactTriggeringPaths(yaml, info.ArtifactName));
        
        // 2. Get service-level triggering paths
        triggeringPaths.AddRange(GetSequenceAsStrings(yaml, "extends", "parameters", "TriggeringPaths"));
        
        // 3. Always include the CI YAML file itself
        var ciYamlRelative = GetRelativePath(ciYamlPath, repoRoot);
        if (!string.IsNullOrEmpty(ciYamlRelative))
        {
            triggeringPaths.Add("/" + ciYamlRelative);
        }

        // Resolve all triggering paths to repo-root-relative format
        var resolvedTriggers = ResolveTriggeringPaths(triggeringPaths, ciYamlDir, repoRoot);

        // Extract additional validation packages from artifact config
        var additionalPackages = GetArtifactAdditionalValidationPackages(yaml, info.ArtifactName);

        // Update the PackageInfo
        info.TriggeringPaths = resolvedTriggers;
        info.AdditionalValidationPackages = additionalPackages;
        info.CiParameters = new CiParameters
        {
            BuildSnippets = buildSnippets,
            CheckAotCompat = checkAotCompat,
            AotTestInputs = checkAotCompat ? aotInputs.Select(e => e.Node).ToList() : [],
            MatrixConfigs = matrixConfigs
        };
    }

    /// <summary>
    /// Returns the CiParameters as a JsonObject for JSON serialization.
    /// Used by PackageInfoTool.BuildPackageInfoJson during migration.
    /// </summary>
    public static JsonObject? GetCiParameters(PackageInfo info)
    {
        return GetCiParametersForPackage(info).ToJson();
    }

    /// <summary>
    /// Creates a CiParameters instance from package info without modifying the PackageInfo.
    /// Used for backward compatibility during migration.
    /// </summary>
    public static CiParameters GetCiParametersForPackage(PackageInfo info)
    {
        if (info.Language != SdkLanguage.DotNet)
        {
            return CiParameters.Default;
        }

        var ciYamlResult = TryFindCiYaml(info);
        if (ciYamlResult == null)
        {
            return new CiParameters
            {
                BuildSnippets = true,
                CheckAotCompat = info.AotCompatOptOut == false,
                AotTestInputs = [],
                MatrixConfigs = []
            };
        }

        var (yaml, _) = ciYamlResult.Value;
        
        var buildSnippets = GetBooleanParameter(yaml, "extends", "parameters", "BuildSnippets") ?? true;
        var aotInputs = GetAotTestInputs(yaml, info.ArtifactName);
        var hasBaselinedWarnings = aotInputs.Any(e => e.HasWarningsFile);
        
        var checkAotCompat = GetBooleanParameter(yaml, "extends", "parameters", "CheckAOTCompat")
            ?? (hasBaselinedWarnings || info.AotCompatOptOut != true);

        var matrixConfigs = new List<JsonObject>();
        AddMatrixConfigs(matrixConfigs, yaml, "extends", "parameters", "MatrixConfigs");
        AddMatrixConfigs(matrixConfigs, yaml, "extends", "parameters", "AdditionalMatrixConfigs");

        return new CiParameters
        {
            BuildSnippets = buildSnippets,
            CheckAotCompat = checkAotCompat,
            AotTestInputs = checkAotCompat ? aotInputs.Select(e => e.Node).ToList() : [],
            MatrixConfigs = matrixConfigs
        };
    }

    private static (YamlMappingNode Yaml, string Path)? TryFindCiYaml(PackageInfo info)
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
            var yaml = YamlHelper.LoadYamlMapping(ciFile);
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

    private static bool MatchesArtifact(YamlMappingNode yaml, string? artifactName, string? group)
    {
        var artifacts = GetYamlSequence(yaml, "extends", "parameters", "Artifacts");
        if (artifacts == null)
        {
            return false;
        }

        foreach (var child in artifacts.Children.OfType<YamlMappingNode>())
        {
            var name = GetMappingScalar(child, "name");
            if (string.IsNullOrWhiteSpace(name) || 
                !string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(group))
            {
                return true;
            }

            var groupId = GetMappingScalar(child, "groupId");
            if (string.Equals(groupId, group, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<(JsonObject Node, bool HasWarningsFile)> GetAotTestInputs(YamlMappingNode yaml, string? artifactName)
    {
        var inputs = GetYamlSequence(yaml, "extends", "parameters", "AOTTestInputs");
        if (inputs == null)
        {
            return [];
        }

        var results = new List<(JsonObject, bool)>();
        
        foreach (var child in inputs.Children.OfType<YamlMappingNode>())
        {
            var name = GetMappingScalar(child, "ArtifactName");
            if (!string.IsNullOrWhiteSpace(artifactName) &&
                !string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var warningsPath = GetMappingScalar(child, "ExpectedWarningsFilePath")
                ?? GetMappingScalar(child, "ExpectedWarningsFilepath");
            
            var jsonNode = YamlHelper.ConvertToJson(child) as JsonObject ?? new JsonObject();
            results.Add((jsonNode, !string.IsNullOrWhiteSpace(warningsPath)));
        }

        return results;
    }

    private static void AddMatrixConfigs(List<JsonObject> destination, YamlMappingNode yaml, params string[] path)
    {
        var sequence = GetYamlSequence(yaml, path);
        if (sequence == null)
        {
            return;
        }

        foreach (var child in sequence.Children)
        {
            if (YamlHelper.ConvertToJson(child) is JsonObject obj)
            {
                destination.Add(obj);
            }
        }
    }

    private static List<string> GetArtifactTriggeringPaths(YamlMappingNode yaml, string? artifactName)
    {
        var artifacts = GetYamlSequence(yaml, "extends", "parameters", "Artifacts");
        if (artifacts == null)
        {
            return [];
        }

        foreach (var child in artifacts.Children.OfType<YamlMappingNode>())
        {
            var name = GetMappingScalar(child, "name");
            if (!string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var triggers = GetMappingSequence(child, "triggeringPaths");
            if (triggers == null)
            {
                return [];
            }

            return triggers.Children
                .OfType<YamlScalarNode>()
                .Select(n => n.Value ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return [];
    }

    private static List<string> GetArtifactAdditionalValidationPackages(YamlMappingNode yaml, string? artifactName)
    {
        var artifacts = GetYamlSequence(yaml, "extends", "parameters", "Artifacts");
        if (artifacts == null)
        {
            return [];
        }

        foreach (var child in artifacts.Children.OfType<YamlMappingNode>())
        {
            var name = GetMappingScalar(child, "name");
            if (!string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var packages = GetMappingSequence(child, "additionalValidationPackages");
            if (packages == null)
            {
                return [];
            }

            return packages.Children
                .OfType<YamlScalarNode>()
                .Select(n => n.Value ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        return [];
    }

    private static List<string> GetSequenceAsStrings(YamlMappingNode yaml, params string[] path)
    {
        var sequence = GetYamlSequence(yaml, path);
        if (sequence == null)
        {
            return [];
        }

        return sequence.Children
            .OfType<YamlScalarNode>()
            .Select(n => n.Value ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    private static List<string> ResolveTriggeringPaths(List<string> paths, string ciYamlDir, string repoRoot)
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
                // Absolute path from repo root
                fullPath = Path.Combine(repoRoot, triggerPath.TrimStart('/'));
            }
            else
            {
                // Relative path from CI YAML directory
                fullPath = Path.Combine(ciYamlDir, triggerPath);
            }

            // Normalize and convert to repo-root-relative
            try
            {
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    fullPath = Path.GetFullPath(fullPath);
                }
                
                var relative = GetRelativePath(fullPath, repoRoot);
                if (!string.IsNullOrEmpty(relative))
                {
                    // Store with leading slash for consistency with PowerShell
                    resolved.Add("/" + relative);
                }
            }
            catch
            {
                // If path resolution fails, skip it
            }
        }

        return resolved.ToList();
    }

    private static string GetRelativePath(string fullPath, string repoRoot)
    {
        try
        {
            var relative = Path.GetRelativePath(repoRoot, fullPath);
            return relative.Replace("\\", "/");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool? GetBooleanParameter(YamlMappingNode yaml, params string[] path)
    {
        var value = YamlHelper.TryGetScalar(yaml, path);
        if (value == null)
        {
            return null;
        }

        return bool.TryParse(value, out var result) ? result : null;
    }

    private static YamlSequenceNode? GetYamlSequence(YamlMappingNode yaml, params string[] path)
    {
        return YamlHelper.TryGetPath(yaml, path) as YamlSequenceNode;
    }

    private static string? GetMappingScalar(YamlMappingNode mapping, string key)
    {
        return YamlHelper.TryGetChild(mapping, key, out var value)
            ? (value as YamlScalarNode)?.Value
            : null;
    }

    private static YamlSequenceNode? GetMappingSequence(YamlMappingNode mapping, string key)
    {
        return YamlHelper.TryGetChild(mapping, key, out var value)
            ? value as YamlSequenceNode
            : null;
    }
}
