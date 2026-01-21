// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Helpers;
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
    public void WritePackageInfoFile_NormalizesPaths()
    {
        var repoRoot = _tempDirectory.DirectoryPath;
        var packageDir = Path.Combine(repoRoot, "sdk", "storage", "storage-blob");
        Directory.CreateDirectory(packageDir);

        var input = new JsonObject
        {
            ["Name"] = "Azure.Storage.Blobs",
            ["Version"] = "1.2.3",
            ["DirectoryPath"] = packageDir,
            ["ReadMePath"] = "sdk/storage/storage-blob/README.md",
            ["ChangeLogPath"] = Path.Combine(packageDir, "CHANGELOG.md")
        };

        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");
        PackageInfoFileWriter.WritePackageInfoFile(input, outputPath, addDevVersion: false, repoRoot);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["DirectoryPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob"));
        Assert.That(output?["ReadMePath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/README.md"));
        Assert.That(output?["ChangeLogPath"]?.ToString(), Is.EqualTo("sdk/storage/storage-blob/CHANGELOG.md"));
    }

    [Test]
    public void WritePackageInfoFile_PreservesExistingVersion_WhenAddingDevVersion()
    {
        var repoRoot = _tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var existing = new JsonObject
        {
            ["Name"] = "Azure.Storage.Blobs",
            ["Version"] = "1.0.0",
            ["DirectoryPath"] = Path.Combine(repoRoot, "sdk", "storage", "storage-blob")
        };
        File.WriteAllText(outputPath, existing.ToJsonString());

        var incoming = new JsonObject
        {
            ["Name"] = "Azure.Storage.Blobs",
            ["Version"] = "2.0.0",
            ["DirectoryPath"] = Path.Combine(repoRoot, "sdk", "storage", "storage-blob")
        };

        PackageInfoFileWriter.WritePackageInfoFile(incoming, outputPath, addDevVersion: true, repoRoot);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.0.0"));
        Assert.That(output?["DevVersion"]?.ToString(), Is.EqualTo("2.0.0"));
    }
}
