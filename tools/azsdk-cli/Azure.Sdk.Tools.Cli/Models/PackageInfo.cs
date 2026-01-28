// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;

namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Plain data model representing inferred information about an Azure SDK package.
/// </summary>
public class PackageInfo
{
    /// <summary>
    /// Absolute path on disk to the root directory of the package.
    /// </summary>
    public required string PackagePath { get; init; }

    /// <summary>
    /// Absolute path to the root of the git repository.
    /// </summary>
    public required string RepoRoot { get; init; }

    /// <summary>
    /// Path of the package relative to sdk/ directory (e.g., "storage/Azure.Storage.Blobs").
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The package name as defined in the manifest file.
    /// </summary>
    public required string? PackageName { get; init; }

    /// <summary>
    /// Azure service name (e.g., storage, keyvault).
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// SDK language (dotnet, java, python, etc.).
    /// </summary>
    public required SdkLanguage Language { get; init; }

    /// <summary>
    /// Current package version string.
    /// </summary>
    public required string? PackageVersion { get; init; }

    /// <summary>
    /// Absolute path to the samples directory.
    /// </summary>
    public required string SamplesDirectory { get; init; }

    /// <summary>
    /// SDK type: management, dataplane, or functions.
    /// </summary>
    public SdkType SdkType { get; init; } = SdkType.Unknown;

    /// <summary>
    /// Artifact name for CI/packaging (usually same as PackageName).
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
    /// Release status from changelog (e.g., "Unreleased" or a date).
    /// </summary>
    public string? ReleaseStatus { get; init; }

    /// <summary>
    /// Whether this is a track 2 (new SDK) package.
    /// </summary>
    public bool IsNewSdk { get; init; }

    /// <summary>
    /// Whether the package is included only for validation (not direct changes).
    /// </summary>
    public bool IncludedForValidation { get; set; }

    /// <summary>
    /// Whether the package opts out of AOT compatibility checks (.NET only).
    /// </summary>
    public bool? AotCompatOptOut { get; init; }

    /// <summary>
    /// Paths that trigger CI for this package when changed.
    /// Paths are relative to repo root with leading slash and use forward slashes.
    /// </summary>
    public List<string> TriggeringPaths { get; set; } = [];

    /// <summary>
    /// Additional packages that should be validated when this package changes.
    /// Paths are relative to repo root.
    /// </summary>
    public List<string> AdditionalValidationPackages { get; set; } = [];

    /// <summary>
    /// CI parameters extracted from ci*.yml.
    /// </summary>
    public CiParameters CiParameters { get; set; } = CiParameters.Default;

    /// <summary>
    /// Directory path relative to repo root (e.g., "sdk/storage/Azure.Storage.Blobs").
    /// </summary>
    public string DirectoryPath => $"sdk/{RelativePath}".Replace("\\", "/");

    /// <summary>
    /// Converts to JSON format expected by CI pipelines.
    /// </summary>
    public JsonObject ToJson()
    {
        var readmePath = Path.Combine(PackagePath, "README.md");
        var changelogPath = Path.Combine(PackagePath, "CHANGELOG.md");

        return new JsonObject
        {
            ["Name"] = PackageName ?? string.Empty,
            ["ArtifactName"] = ArtifactName ?? PackageName ?? string.Empty,
            ["Version"] = PackageVersion ?? string.Empty,
            ["DirectoryPath"] = DirectoryPath,
            ["ServiceDirectory"] = ServiceDirectory ?? GetServiceDirectoryFromPath(),
            ["ReadMePath"] = File.Exists(readmePath) ? GetRelativePath(readmePath) : string.Empty,
            ["ChangeLogPath"] = File.Exists(changelogPath) ? GetRelativePath(changelogPath) : string.Empty,
            ["Group"] = Group,
            ["SdkType"] = SdkType switch
            {
                SdkType.Management => "mgmt",
                SdkType.Dataplane => "client",
                SdkType.Functions => "functions",
                _ => string.Empty
            },
            ["IsNewSdk"] = IsNewSdk,
            ["ReleaseStatus"] = ReleaseStatus ?? string.Empty,
            ["IncludedForValidation"] = IncludedForValidation,
            ["AdditionalValidationPackages"] = null,
            ["ArtifactDetails"] = null,
            ["CIParameters"] = CiParameters.ToJson(),
            ["DevVersion"] = null
        };
    }

    private string GetRelativePath(string absolutePath)
    {
        return Path.GetRelativePath(RepoRoot, absolutePath).Replace("\\", "/");
    }

    private string? GetServiceDirectoryFromPath()
    {
        if (string.IsNullOrEmpty(RelativePath))
        {
            return null;
        }

        var segments = RelativePath.Replace("\\", "/").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        // Go uses nested service directories like "resourcemanager/compute"
        if (Language == SdkLanguage.Go && segments.Length >= 2)
        {
            return $"{segments[0]}/{segments[1]}";
        }

        return segments[0];
    }
}
