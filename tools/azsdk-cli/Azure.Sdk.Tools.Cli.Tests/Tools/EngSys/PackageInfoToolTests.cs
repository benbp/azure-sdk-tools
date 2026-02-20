// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Moq;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Telemetry;
using Azure.Sdk.Tools.Cli.Tools.EngSys;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tests.Mocks.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoToolTests
{
    private PackageInfoTool tool;

    [SetUp]
    public void Setup()
    {
        var githubHelper = new Mock<GitHelper>();
        var languageService = new Mock<LanguageService>();
        var languageServices = new List<LanguageService> { languageService.Object };
        var logger = new TestLogger<PackageInfoTool>();
        var outputHelper = new OutputHelper(OutputHelper.OutputModes.Hidden);
        tool = new PackageInfoTool(githubHelper.Object, logger, languageServices);
        tool.Initialize(outputHelper, new Mock<ITelemetryService>().Object, new MockUpgradeService());
    }

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

        var result = tool.FilterPackagesByArtifact(packages, ["artifact-b"]);

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

        var warnings = new List<string>();
        var result = tool.FilterPackagesByArtifact(packages, []);

        Assert.That(result, Has.Count.EqualTo(1));
    }
}
