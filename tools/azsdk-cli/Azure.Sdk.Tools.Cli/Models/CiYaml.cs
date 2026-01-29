// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Model for deserializing Azure DevOps CI YAML files.
/// Only includes fields relevant to package info extraction.
/// </summary>
internal class CiYaml
{
    [YamlMember(Alias = "extends")]
    public CiYamlExtends? Extends { get; set; }
}

internal class CiYamlExtends
{
    [YamlMember(Alias = "parameters")]
    public CiYamlParameters? Parameters { get; set; }
}

internal class CiYamlParameters
{
    [YamlMember(Alias = "BuildSnippets")]
    public bool? BuildSnippets { get; set; }

    [YamlMember(Alias = "CheckAOTCompat")]
    public bool? CheckAotCompat { get; set; }

    [YamlMember(Alias = "AOTTestInputs")]
    public List<CiYamlAotTestInput>? AotTestInputs { get; set; }

    [YamlMember(Alias = "MatrixConfigs")]
    public List<Dictionary<string, object>>? MatrixConfigs { get; set; }

    [YamlMember(Alias = "AdditionalMatrixConfigs")]
    public List<Dictionary<string, object>>? AdditionalMatrixConfigs { get; set; }

    [YamlMember(Alias = "TriggeringPaths")]
    public List<string>? TriggeringPaths { get; set; }

    [YamlMember(Alias = "Artifacts")]
    public List<CiYamlArtifact>? Artifacts { get; set; }
}

internal class CiYamlAotTestInput
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

internal class CiYamlArtifact
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
