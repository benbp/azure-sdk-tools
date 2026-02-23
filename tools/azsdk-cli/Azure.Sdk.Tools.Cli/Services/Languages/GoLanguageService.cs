// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Language-specific helper for Go packages. Provides structural package info plus lazy accessors
/// for samples directory, file extension, and version parsing.
/// </summary>
public partial class GoLanguageService : LanguageService
{
    public GoLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IPackageInfoHelper packageInfoHelper,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, packageInfoHelper, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    private readonly string goUnix = "go";
    private readonly string goWin = "go.exe";
    private readonly string gofmtUnix = "gofmt";
    private readonly string gofmtWin = "gofmt.exe";
    private readonly string golangciLintUnix = "golangci-lint";
    private readonly string golangciLintWin = "golangci-lint.exe";
    private readonly IPowershellHelper powershellHelper;

    // Known locations for Go customization files
    private const string CustomizationPathInternalGenerate = "internal/generate";
    private const string CustomizationPathTestdataGenerate = "testdata/generate";

    public override SdkLanguage Language { get; } = SdkLanguage.Go;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// Go packages are identified by go.mod files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["go.mod"];

    /// <summary>
    /// Discovers all packages in a service directory with CI parameters populated.
    /// </summary>
    public override async Task<IReadOnlyList<PackageInfo>> DiscoverPackagesAsync(
        string repoRoot,
        string? serviceDirectory,
        CancellationToken ct = default)
    {
        var packages = await base.DiscoverPackagesAsync(repoRoot, serviceDirectory, ct);

        // Populate CI parameters for each package
        foreach (var package in packages)
        {
            PopulateGoCiParameters(package);
        }

        return packages;
    }

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        var fullPath = RealPath.GetRealPath(packagePath);
        var repoRoot = await gitHelper.DiscoverRepoRootAsync(packagePath, ct);
        var sdkRoot = Path.Combine(repoRoot, "sdk");

        try
        {
            var commonPS1 = Path.Join(repoRoot, "eng", "common", "scripts", "common.ps1");

            // The powershell outputs this:
            // {
            //   "Name": "sdk/messaging/azservicebus",
            //   "Version": "1.10.1-beta.1",
            //   "DirectoryPath": "../sdk/messaging/azservicebus",
            //   "ServiceDirectory": "messaging/azservicebus",
            //   "ReadMePath": "../sdk/messaging/azservicebus/README.md",
            //   "ChangeLogPath": "../sdk/messaging/azservicebus/CHANGELOG.md",
            //   "SdkType": "client",
            //   "IsNewSdk": true,
            //   "ReleaseStatus": "Unreleased",
            //   "IncludedForValidation": false,
            //   "CIParameters": {
            //     "CIMatrixConfigs": []
            //   },
            //   "VersionFile": "/home/ripark/src/az/sdk/messaging/azservicebus/internal/constants.go",
            //   "ModuleName": "azservicebus"
            // }
            logger.LogDebug("Resolving Go package info for path: {packagePath}", packagePath);
            string[] args = [$". {commonPS1}; Get-GoModuleProperties('{packagePath}') | ConvertTo-Json"];
            var processResult = await processHelper.Run(new PowershellOptions(args, workingDirectory: repoRoot), ct);

            if (processResult.ExitCode != 0)
            {
                throw new Exception($"Failed to extract package properties for {packagePath}: {processResult.Output}");
            }

            GoModulePropertiesPowershell goModuleProperties;

            try
            {
                goModuleProperties = JsonSerializer.Deserialize<GoModulePropertiesPowershell>(processResult.Stdout)
                    ?? throw new Exception($"Failed to deserialize results from Get-GoModuleProperties.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to deserialized JSON output '{processResult.Stdout}'", ex);
            }

            var relativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar);
            var directoryPath = $"sdk/{relativePath}";

            var model = new PackageInfo
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = relativePath,
                PackageName = goModuleProperties.Name,
                PackageVersion = goModuleProperties.Version,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                SdkTypeString = goModuleProperties.SdkType ?? string.Empty,
                Language = SdkLanguage.Go,
                SamplesDirectory = fullPath,
                // Map additional fields from PowerShell
                DirectoryPath = directoryPath,
                ServiceDirectory = goModuleProperties.ServiceDirectory,
                ReadMePath = !string.IsNullOrEmpty(goModuleProperties.ReadMePath) ? $"{directoryPath}/README.md" : string.Empty,
                ChangeLogPath = !string.IsNullOrEmpty(goModuleProperties.ChangeLogPath) ? $"{directoryPath}/CHANGELOG.md" : string.Empty,
                IsNewSdk = goModuleProperties.IsNewSdk,
                ArtifactName = goModuleProperties.ArtifactName ?? goModuleProperties.Name,
                ReleaseStatus = goModuleProperties.ReleaseStatus ?? string.Empty,
                SpecProjectPath = GetSpecProjectPath(fullPath)
            };

            logger.LogDebug("Resolved Go package: {packageName} v{packageVersion}", model.PackageName ?? "(unknown)", model.PackageVersion ?? "(unknown)");
            return model;
        }
        catch (Exception ex)
        {
            // NOTE: this method, via the LanguageService, cannot throw these exceptions, so we only log it.
            logger.LogDebug(ex, "Exception thrown when trying to get package properties for {Path}", packagePath);
            return new PackageInfo()
            {
                PackagePath = fullPath,
                RepoRoot = repoRoot,
                RelativePath = Path.GetRelativePath(sdkRoot, fullPath).TrimStart(Path.DirectorySeparatorChar),
                PackageName = null,
                PackageVersion = null,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                SdkType = SdkType.Unknown,
                Language = SdkLanguage.Go,
                SamplesDirectory = fullPath
            };
        }
    }

    public override bool HasCustomizations(string packagePath, CancellationToken ct)
    {
        // Go customization files can live in different locations depending on the package.
        // Known locations include:
        //   - internal/generate (most common)
        //   - testdata/generate (e.g., azcertificates)
        // TODO: In the future, check tspconfig.yaml for "go-generate" directive for definitive detection.

        try
        {
            string[] knownLocations = [CustomizationPathInternalGenerate, CustomizationPathTestdataGenerate];

            foreach (var location in knownLocations)
            {
                var customizationPath = Path.Combine(packagePath, location);
                if (Directory.Exists(customizationPath))
                {
                    logger.LogDebug("Found Go customization directory at {CustomizationPath}", customizationPath);
                    return true;
                }
            }

            logger.LogDebug("No Go customization directory found in {PackagePath}", packagePath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for Go customization files in {PackagePath}", packagePath);
            return false;
        }
    }

    /// <summary>
    /// Populates Go-specific CI parameters from ci.yml.
    /// Go CI parameters: LicenseCheck, NonShipping, UsePipelineProxy, IsSdkLibrary
    /// </summary>
    private void PopulateGoCiParameters(PackageInfo info)
    {
        // Default Go CI parameters
        info.CiParameters = new CiPipelineParameters
        {
            LicenseCheck = true,
            NonShipping = false,
            UsePipelineProxy = true,
            IsSdkLibrary = true
        };

        if (string.IsNullOrWhiteSpace(info.ServiceDirectory))
        {
            return;
        }

        // Try to find and parse ci.yml
        var ciYamlPath = Path.Combine(info.RepoRoot, "sdk", info.ServiceDirectory, "ci.yml");
        if (!File.Exists(ciYamlPath))
        {
            return;
        }

        try
        {
            var yaml = ParseGoCiYaml(ciYamlPath);
            if (yaml?.Extends?.Parameters == null)
            {
                return;
            }

            var parameters = yaml.Extends.Parameters;

            // Override defaults with values from ci.yml if present
            if (parameters.LicenseCheck.HasValue)
            {
                info.CiParameters.LicenseCheck = parameters.LicenseCheck.Value;
            }
            if (parameters.NonShipping.HasValue)
            {
                info.CiParameters.NonShipping = parameters.NonShipping.Value;
            }
            if (parameters.UsePipelineProxy.HasValue)
            {
                info.CiParameters.UsePipelineProxy = parameters.UsePipelineProxy.Value;
            }
            if (parameters.IsSdkLibrary.HasValue)
            {
                info.CiParameters.IsSdkLibrary = parameters.IsSdkLibrary.Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse Go ci.yml at {Path}", ciYamlPath);
        }
    }

    private static readonly IDeserializer GoCiYamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static GoCiPipelineYaml? ParseGoCiYaml(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            return GoCiYamlDeserializer.Deserialize<GoCiPipelineYaml>(reader);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// These are the properties that come out of the Get-GoModuleProperties powershell func.
    /// </summary>
    private record GoModulePropertiesPowershell(
        string? Name,
        string? Version,
        string? DirectoryPath,
        string? ServiceDirectory,
        string? ReadMePath,
        string? ChangeLogPath,
        string? SdkType,
        bool IsNewSdk,
        string? ArtifactName,
        string? ReleaseStatus);

    /// <summary>
    /// Go CI pipeline YAML structure for parsing ci.yml.
    /// </summary>
    private class GoCiPipelineYaml
    {
        [YamlMember(Alias = "extends")]
        public GoCiPipelineYamlExtends? Extends { get; set; }
    }

    private class GoCiPipelineYamlExtends
    {
        [YamlMember(Alias = "parameters")]
        public GoCiPipelineYamlParameters? Parameters { get; set; }
    }

    private class GoCiPipelineYamlParameters
    {
        public bool? LicenseCheck { get; set; }
        public bool? NonShipping { get; set; }
        public bool? UsePipelineProxy { get; set; }
        public bool? IsSdkLibrary { get; set; }
    }
}
