// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoArtifactFilterTests
{
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

        var result = PackageInfoArtifactFilter.FilterPackagesByArtifact(packages, ["artifact-b"]);

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

        var result = PackageInfoArtifactFilter.FilterPackagesByArtifact(packages, []);

        Assert.That(result, Has.Count.EqualTo(1));
    }
}
