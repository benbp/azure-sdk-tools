// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Azure.Sdk.Tools.Cli.Helpers;

public static class PackageInfoFileWriter
{
    private static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true
    };

    public static void WritePackageInfoFile(JsonObject incomingPackageSpec, string outputPath, bool addDevVersion, string repoRoot)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        JsonObject outputObject;
        if (File.Exists(outputPath))
        {
            var existingJson = File.ReadAllText(outputPath);
            outputObject = JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject();
        }
        else
        {
            outputObject = incomingPackageSpec.DeepClone() as JsonObject ?? new JsonObject();
        }

        if (addDevVersion)
        {
            outputObject["DevVersion"] = incomingPackageSpec["Version"]?.ToString();
        }

        SetRelativePath(outputObject, "DirectoryPath", repoRoot);
        SetRelativePath(outputObject, "ReadMePath", repoRoot);
        SetRelativePath(outputObject, "ChangeLogPath", repoRoot);

        File.WriteAllText(outputPath, JsonSerializer.Serialize(outputObject, serializerOptions));
    }

    private static void SetRelativePath(JsonObject outputObject, string propertyName, string repoRoot)
    {
        if (outputObject[propertyName] is null)
        {
            outputObject[propertyName] = string.Empty;
            return;
        }

        var raw = outputObject[propertyName]?.ToString();
        if (string.IsNullOrEmpty(raw))
        {
            outputObject[propertyName] = string.Empty;
            return;
        }

        if (!Path.IsPathRooted(raw))
        {
            outputObject[propertyName] = raw;
            return;
        }

        var relativePath = Path.GetRelativePath(repoRoot, raw).Replace("\\", "/");
        outputObject[propertyName] = relativePath;
    }
}
