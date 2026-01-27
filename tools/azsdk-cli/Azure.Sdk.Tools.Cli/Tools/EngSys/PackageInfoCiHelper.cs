// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

internal static class PackageInfoCiHelper
{
    public static JsonObject GetCiParameters(PackageInfo info)
    {
        var parameters = new JsonObject
        {
            ["CIMatrixConfigs"] = new JsonArray()
        };

        if (info.Language != SdkLanguage.DotNet)
        {
            return parameters;
        }

        var ciYaml = TryGetCiYaml(info);
        if (ciYaml == null)
        {
            parameters["CheckAOTCompat"] = info.AotCompatOptOut == false;
            parameters["AOTTestInputs"] = new JsonArray();
            parameters["BuildSnippets"] = true;
            return parameters;
        }

        var buildSnippets = TryGetBoolean(ciYaml, "extends", "parameters", "BuildSnippets");
        parameters["BuildSnippets"] = buildSnippets ?? true;

        var checkAotCompat = TryGetBoolean(ciYaml, "extends", "parameters", "CheckAOTCompat");
        var aotInputs = TryGetAotTestInputs(ciYaml, info.ArtifactName);
        var hasBaselinedWarnings = aotInputs.Any(entry => entry.HasExpectedWarningsFilePath);

        var shouldCheckAot = checkAotCompat
                             ?? (hasBaselinedWarnings
                                 ? true
                                 : info.AotCompatOptOut != true);

        parameters["CheckAOTCompat"] = shouldCheckAot;
        parameters["AOTTestInputs"] = shouldCheckAot
            ? ConvertYamlEntriesToJson(aotInputs.Select(entry => entry.Node))
            : new JsonArray();

        var matrixConfigs = TryGetYamlSequence(ciYaml, "extends", "parameters", "MatrixConfigs");
        var additionalMatrixConfigs = TryGetYamlSequence(ciYaml, "extends", "parameters", "AdditionalMatrixConfigs");
        var matrixArray = new JsonArray();
        AddYamlSequenceItems(matrixArray, matrixConfigs);
        AddYamlSequenceItems(matrixArray, additionalMatrixConfigs);
        if (matrixArray.Count > 0)
        {
            parameters["CIMatrixConfigs"] = matrixArray;
        }

        return parameters;
    }

    private static YamlMappingNode? TryGetCiYaml(PackageInfo info)
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
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ciFiles.Length == 0)
        {
            return null;
        }

        var sole = ciFiles.Length == 1;
        foreach (var ciFile in ciFiles)
        {
            var yaml = YamlHelper.LoadYamlMapping(ciFile);
            if (yaml == null)
            {
                continue;
            }

            if (sole || MatchesArtifact(yaml, info.ArtifactName, info.Group))
            {
                return yaml;
            }
        }

        return null;
    }

    private static bool MatchesArtifact(YamlMappingNode yaml, string? artifactName, string? group)
    {
        var artifactsNode = TryGetYamlSequence(yaml, "extends", "parameters", "Artifacts");
        if (artifactsNode == null)
        {
            return false;
        }

        foreach (var child in artifactsNode.Children.OfType<YamlMappingNode>())
        {
            var name = TryGetMappingScalar(child, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrEmpty(group))
            {
                return true;
            }

            var groupId = TryGetMappingScalar(child, "groupId");
            if (string.Equals(groupId, group, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<(YamlMappingNode Node, bool HasExpectedWarningsFilePath)> TryGetAotTestInputs(YamlMappingNode yaml, string? artifactName)
    {
        var inputsNode = TryGetYamlSequence(yaml, "extends", "parameters", "AOTTestInputs");
        var entries = new List<(YamlMappingNode, bool)>();
        if (inputsNode == null)
        {
            return entries;
        }

        foreach (var child in inputsNode.Children.OfType<YamlMappingNode>())
        {
            var name = TryGetMappingScalar(child, "ArtifactName");
            if (!string.IsNullOrWhiteSpace(artifactName) &&
                !string.Equals(name, artifactName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var expectedWarnings = TryGetMappingScalar(child, "ExpectedWarningsFilePath")
                                   ?? TryGetMappingScalar(child, "ExpectedWarningsFilepath");
            entries.Add((child, !string.IsNullOrWhiteSpace(expectedWarnings)));
        }

        return entries;
    }

    private static JsonArray ConvertYamlEntriesToJson(IEnumerable<YamlMappingNode> nodes)
    {
        var array = new JsonArray();
        foreach (var node in nodes)
        {
            array.Add(YamlHelper.ConvertToJson(node));
        }
        return array;
    }

    private static void AddYamlSequenceItems(JsonArray destination, YamlSequenceNode? sequence)
    {
        if (sequence == null)
        {
            return;
        }

        foreach (var child in sequence.Children)
        {
            destination.Add(child == null ? null : YamlHelper.ConvertToJson(child));
        }
    }

    private static bool? TryGetBoolean(YamlMappingNode yaml, params string[] path)
    {
        var value = YamlHelper.TryGetScalar(yaml, path);
        if (value == null)
        {
            return null;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static YamlSequenceNode? TryGetYamlSequence(YamlMappingNode yaml, params string[] path)
    {
        var node = YamlHelper.TryGetPath(yaml, path);
        return node as YamlSequenceNode;
    }

    private static string? TryGetMappingScalar(YamlMappingNode mapping, string key)
    {
        return YamlHelper.TryGetChild(mapping, key, out var value)
            ? (value as YamlScalarNode)?.Value
            : null;
    }
}
