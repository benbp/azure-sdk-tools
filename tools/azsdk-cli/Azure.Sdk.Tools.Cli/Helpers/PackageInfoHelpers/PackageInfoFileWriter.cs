// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;

public static class PackageInfoFileWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static void WritePackageInfoFile(PackageInfo packageInfo, string outputPath, bool addDevVersion)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (addDevVersion)
        {
            packageInfo.DevVersion = packageInfo.PackageVersion;
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(packageInfo, SerializerOptions));
    }
}
