// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Plain data model representing inferred information about an Azure SDK package.
/// </summary>
public class PackageInfo
{
    /// <summary>
    /// Absolute path on disk to the root directory of the package that is being inspected / operated on.
    /// </summary>
    /// <remarks>
    /// This is typically a directory under the cloned azure-sdk-* repository, e.g. a path like
    /// <c>/home/user/azure-sdk-for-js/sdk/storage/storage-blob</c>.
    /// </remarks>
    public required string PackagePath { get; init; }

    /// <summary>
    /// Absolute path to the root of the git repository that contains the package (e.g. <c>azure-sdk-for-js</c>).
    /// </summary>
    /// <remarks>
    /// Useful when computing relative paths or when running repository level tooling (git operations, global config lookup, etc.).
    /// </remarks>
    public required string RepoRoot { get; init; }

    /// <summary>
    /// Path of the package relative to <see cref="RepoRoot"/> (no leading directory separators).
    /// </summary>
    /// <example><c>sdk/storage/storage-blob</c></example>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The actual package name as defined in the language-specific manifest file.
    /// </summary>
    public required string? PackageName { get; init; }

    /// <summary>
    /// Logical Azure service name that the package targets (e.g. <c>storage</c>, <c>keyvault</c>, <c>cosmos</c>).
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// Language moniker (e.g. <c>typescript</c>, <c>dotnet</c>, <c>python</c>, <c>java</c>, <c>go</c> ).
    /// </summary>
    /// <remarks>
    /// Used for selecting language specific strategies (sample folder layout, file extensions, version extraction, etc.).
    /// </remarks>
    public required SdkLanguage Language { get; init; }

    /// <summary>
    /// The current package version string, or <c>null</c> if it cannot be determined.
    /// </summary>
    public required string? PackageVersion { get; init; }

    /// <summary>
    /// The absolute path to the directory containing runnable samples for the package.
    /// </summary>
    public required string SamplesDirectory { get; init; }

    /// <summary>
    /// SDK type : management plane or data plane.
    /// </summary>
    public SdkType SdkType { get; set; } = SdkType.Unknown;

    /// <summary>
    /// Optional artifact name used for CI and packaging metadata.
    /// </summary>
    public string? ArtifactName { get; init; }

    /// <summary>
    /// Service directory under sdk/ (may include group/service for Go).
    /// </summary>
    public string? ServiceDirectory { get; init; }

    /// <summary>
    /// Optional group identifier (e.g., Maven groupId).
    /// </summary>
    public string? Group { get; init; }

    /// <summary>
    /// Optional spec project path (e.g., from tsp-location.yaml).
    /// </summary>
    public string? SpecProjectPath { get; init; }

    /// <summary>
    /// Release status derived from changelog (e.g., "Unreleased" or a date).
    /// </summary>
    public string? ReleaseStatus { get; init; }

    /// <summary>
    /// Indicates whether the package is a track 2 (new SDK) package.
    /// </summary>
    public bool IsNewSdk { get; init; }

    /// <summary>
    /// Indicates whether the package is included only for validation.
    /// </summary>
    public bool IncludedForValidation { get; init; }
}
