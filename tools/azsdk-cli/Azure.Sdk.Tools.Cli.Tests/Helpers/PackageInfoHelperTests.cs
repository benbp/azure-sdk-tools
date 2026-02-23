// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Nodes;
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoHelperTests
{
    private PackageInfoHelper packageInfoHelper;
    private TempDirectory? tempDirectory;

    [SetUp]
    public void Setup()
    {
        var logger = new TestLogger<PackageInfoHelper>();
        var gitHelper = new Mock<IGitHelper>();
        packageInfoHelper = new PackageInfoHelper(logger, gitHelper.Object);
    }

    [TearDown]
    public void TearDown() => tempDirectory?.Dispose();

    [Test]
    public void FilterByArtifacts_ReturnsFilteredMatches()
    {
        var packages = new List<PackageInfo>
        {
            new()
            {
                PackageName = "PackageA",
                ArtifactName = "artifact-a"
            },
            new()
            {
                PackageName = "PackageB",
                ArtifactName = "artifact-b"
            }
        };

        var result = packageInfoHelper.FilterPackagesByArtifact(packages, ["artifact-b"]);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].PackageName, Is.EqualTo("PackageB"));
    }

    [Test]
    public void FilterByArtifacts_UsesNoFilterWhenListIsEmpty()
    {
        var packages = new List<PackageInfo>
        {
            new()
            {
                PackageName = "PackageA",
                ArtifactName = "artifact-a"
            }
        };

        var result = packageInfoHelper.FilterPackagesByArtifact(packages, []);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public void WritePackageInfoFile_WritesJsonWithCorrectFormat()
    {
        tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_WritesJsonWithCorrectFormat));
        var repoRoot = tempDirectory.DirectoryPath;
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
        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

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
        var tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_SetsDevVersion_WhenAddDevVersionIsTrue));
        var repoRoot = tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            ArtifactName = "Azure.Storage.Blobs",
            PackageVersion = "2.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: true);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("2.0.0"));
        Assert.That(output?["DevVersion"]?.ToString(), Is.EqualTo("2.0.0"));
    }

    [Test]
    public void WritePackageInfoFile_DoesNotSetDevVersion_WhenAddDevVersionIsFalse()
    {
        var tempDirectory = TempDirectory.Create(nameof(WritePackageInfoFile_DoesNotSetDevVersion_WhenAddDevVersionIsFalse));
        var repoRoot = tempDirectory.DirectoryPath;
        var outputPath = Path.Combine(repoRoot, "out", "Azure.Storage.Blobs.json");

        var packageInfo = new PackageInfo
        {
            PackageName = "Azure.Storage.Blobs",
            PackageVersion = "1.0.0",
            DirectoryPath = "sdk/storage/storage-blob"
        };

        packageInfoHelper.WritePackageInfoFile(packageInfo, outputPath, addDevVersion: false);

        var output = JsonNode.Parse(File.ReadAllText(outputPath)) as JsonObject;
        Assert.That(output, Is.Not.Null);
        Assert.That(output?["Version"]?.ToString(), Is.EqualTo("1.0.0"));
        Assert.That(output?["DevVersion"], Is.Null);
    }
}
