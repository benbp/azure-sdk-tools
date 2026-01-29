// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// CI pipeline parameters extracted from ci*.yml files.
/// </summary>
public class CiParameters
{
    /// <summary>
    /// Whether to build code snippets from samples. Defaults to true (opt-out).
    /// </summary>
    [JsonPropertyName("BuildSnippets")]
    public bool BuildSnippets { get; set; } = true;

    /// <summary>
    /// Whether to run AOT compatibility checks.
    /// </summary>
    [JsonPropertyName("CheckAOTCompat")]
    public bool CheckAotCompat { get; set; }

    /// <summary>
    /// AOT test input configurations for the package.
    /// </summary>
    [JsonPropertyName("AOTTestInputs")]
    public List<Dictionary<string, object?>> AotTestInputs { get; set; } = [];

    /// <summary>
    /// CI matrix configurations (MatrixConfigs + AdditionalMatrixConfigs).
    /// </summary>
    [JsonPropertyName("CIMatrixConfigs")]
    public List<Dictionary<string, object?>> MatrixConfigs { get; set; } = [];

    /// <summary>
    /// Default CI parameters when no ci*.yml is found.
    /// </summary>
    public static CiParameters Default => new();
}
