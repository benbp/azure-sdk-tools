// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// CI pipeline parameters extracted from ci*.yml files.
/// </summary>
public record CiParameters
{
    /// <summary>
    /// Whether to build code snippets from samples. Defaults to true (opt-out).
    /// </summary>
    public bool BuildSnippets { get; init; } = true;

    /// <summary>
    /// Whether to run AOT compatibility checks.
    /// </summary>
    public bool CheckAotCompat { get; init; }

    /// <summary>
    /// AOT test input configurations for the package.
    /// </summary>
    public List<JsonObject> AotTestInputs { get; init; } = [];

    /// <summary>
    /// CI matrix configurations (MatrixConfigs + AdditionalMatrixConfigs).
    /// </summary>
    public List<JsonObject> MatrixConfigs { get; init; } = [];

    /// <summary>
    /// Converts to JSON format expected by CI pipelines.
    /// </summary>
    public JsonObject ToJson()
    {
        var matrixArray = new JsonArray();
        foreach (var config in MatrixConfigs)
        {
            matrixArray.Add(config.DeepClone());
        }

        var aotArray = new JsonArray();
        foreach (var input in AotTestInputs)
        {
            aotArray.Add(input.DeepClone());
        }

        return new JsonObject
        {
            ["BuildSnippets"] = BuildSnippets,
            ["CheckAOTCompat"] = CheckAotCompat,
            ["AOTTestInputs"] = aotArray,
            ["CIMatrixConfigs"] = matrixArray
        };
    }

    /// <summary>
    /// Default CI parameters when no ci*.yml is found.
    /// </summary>
    public static CiParameters Default => new();
}
