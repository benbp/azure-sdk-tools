// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Model for deserializing Azure DevOps CI pipeline YAML files (ci*.yml).
/// Only includes fields relevant to package info extraction.
/// </summary>
internal class CiPipelineYaml
{
    [YamlMember(Alias = "extends")]
    public CiPipelineYamlExtends? Extends { get; set; }
}

internal class CiPipelineYamlExtends
{
    [YamlMember(Alias = "parameters")]
    public CiPipelineYamlParameters? Parameters { get; set; }
}

internal class CiPipelineYamlParameters
{
    [YamlMember(Alias = "BuildSnippets")]
    public bool? BuildSnippets { get; set; }

    [YamlMember(Alias = "CheckAOTCompat")]
    public bool? CheckAotCompat { get; set; }

    [YamlMember(Alias = "AOTTestInputs")]
    public List<CiPipelineYamlAotTestInput>? AotTestInputs { get; set; }

    [YamlMember(Alias = "MatrixConfigs")]
    public List<Dictionary<string, object>>? MatrixConfigs { get; set; }

    [YamlMember(Alias = "AdditionalMatrixConfigs")]
    public List<Dictionary<string, object>>? AdditionalMatrixConfigs { get; set; }

    [YamlMember(Alias = "TriggeringPaths")]
    public List<string>? TriggeringPaths { get; set; }

    [YamlMember(Alias = "Artifacts")]
    public List<CiPipelineYamlArtifact>? Artifacts { get; set; }
}

internal class CiPipelineYamlAotTestInput
{
    [YamlMember(Alias = "ArtifactName")]
    public string? ArtifactName { get; set; }

    [YamlMember(Alias = "ExpectedWarningsFilePath")]
    public string? ExpectedWarningsFilePath { get; set; }

    [YamlMember(Alias = "ExpectedWarningsFilepath")]
    public string? ExpectedWarningsFilepathAlt { get; set; }

    /// <summary>
    /// Gets the warnings file path from either casing variant.
    /// </summary>
    public string? WarningsFilePath => ExpectedWarningsFilePath ?? ExpectedWarningsFilepathAlt;

    /// <summary>
    /// Indicates whether a warnings file is configured (and not "None").
    /// </summary>
    public bool HasWarningsFile =>
        !string.IsNullOrWhiteSpace(WarningsFilePath) &&
        !string.Equals(WarningsFilePath, "None", StringComparison.OrdinalIgnoreCase);
}

internal class CiPipelineYamlArtifact
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "groupId")]
    public string? GroupId { get; set; }

    [YamlMember(Alias = "triggeringPaths")]
    public List<string>? TriggeringPaths { get; set; }

    [YamlMember(Alias = "additionalValidationPackages")]
    public List<string>? AdditionalValidationPackages { get; set; }
}
