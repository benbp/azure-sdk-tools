// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoFileWriterTests
{
    private TempDirectory _tempDirectory = null!;

    [SetUp]
    public void SetUp() => _tempDirectory = TempDirectory.Create(nameof(PackageInfoFileWriterTests));

    [TearDown]
    public void TearDown() => _tempDirectory.Dispose();

    [Test]
    public void WritePackageInfoFile_WritesJsonWithCorrectFormat()
    {
        var repoRoot = _tempDirectory.DirectoryPath;
        var packageDir = Path.Combine(repoRoot, "sdk", "storage", "storage-blob");
        Directory.CreateDirectory(packageDir);

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            ArtifactName = "Azure.Storage.Blobs",
            PackageVersion = "1.2.3",
            DirectoryPath = "sdk/storage/storage-blob",
            ReadMePath = "sdk/storage/storage-blob/README.md",
            ChangeLogPath = "sdk/storage/storage-blob/CHANGELOG.md"
        };

        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");
        PackageInfoFileWriter.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Name"]?.ToString(), Is.EqualTo("Azure.Storage.Blobs"));
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.2.3"));
        Assert.That(output?["DirectoryPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob"));
        Assert.That(output?["ReadMePath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/README.md"));
        Assert.That(output?["ChangeLogPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/CHANGELOG.md"));
    }

    [Test]
    public void WritePackageInfoFile_SetsDevVersion_WhenAddDevVersionIsTrue()
    {
        var repoRoot = _tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            ArtifactName = "Azure.Storage.Blobs",
            PackageVersion = "2.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        PackageInfoFileWriter.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: true);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("2.0.0"));
        Assert.That(output?["DevVersion"]?.ToString(), Is.EqualTo("2.0.0"));
    }

    [Test]
    public void WritePackageInfoFile_DoesNotSetDevVersion_WhenAddDevVersionIsFalse()
    {
        var repoRoot = _tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            PackageVersion = "1.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        PackageInfoFileWriter.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.0.0"));
        Assert.That(output?["DevVersion"], Is.Null);
    }
}
