// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Nodes;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

public class PackageInfoArtifactFilterTests
{
    [Test]
    public void FilterByArtifacts_ReturnsFilteredMatches()
    {
        var packages = new List<JsonObject>
        {
            new()
            {
                ["Name"] = "PackageA",
                ["ArtifactName"] = "artifact-a"
            },
            new()
            {
                ["Name"] = "PackageB",
                ["ArtifactName"] = "artifact-b"
            }
        };

        var warnings = new List<string>();
        var result = PackageInfoArtifactFilter.FilterByArtifacts(packages, ["artifact-b"], warnings.Add);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0]["Name"]?.ToString(), Is.EqualTo("PackageB"));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void FilterByArtifacts_UsesNoFilterWhenListIsEmpty()
    {
        var packages = new List<JsonObject>
        {
            new()
            {
                ["Name"] = "PackageA",
                ["ArtifactName"] = "artifact-a"
            }
        };

        var warnings = new List<string>();
        var result = PackageInfoArtifactFilter.FilterByArtifacts(packages, Array.Empty<string>(), warnings.Add);

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(warnings, Is.Empty);
    }
}
