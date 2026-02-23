// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed partial class DotnetLanguageService : LanguageService
{
    private const string DotNetCommand = "dotnet";
    private const string RequiredDotNetVersion = "9.0.102"; // TODO - centralize this as part of env setup tool
    private const string GeneratedFolderName = "Generated";
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);
    private static readonly string[] MsBuildOutputSeparator = ["' '"];

    private readonly IPowershellHelper powershellHelper;

    public DotnetLanguageService(
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

    public override SdkLanguage Language => SdkLanguage.DotNet;
    public override bool IsCustomizedCodeUpdateSupported => true;

    /// <summary>
    /// .NET packages are identified by .csproj files.
    /// </summary>
    protected override string[] PackageManifestPatterns => ["*.csproj"];

    /// <summary>
    /// For .NET, the .csproj is typically in a src/ or test/ subdirectory.
    /// The package root is the parent of that directory.
    /// </summary>
    protected override string? GetPackageRootFromManifest(string manifestPath)
    {
        var directory = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        var directoryName = new DirectoryInfo(directory).Name;
        if (string.Equals(directoryName, "src", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(directoryName, "test", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(directory)?.FullName;
        }

        return directory;
    }

    /// <summary>
    /// Discovers all packages in a service directory with CI parameters populated.
    /// </summary>
    public override async Task<IReadOnlyList<PackageInfo>> DiscoverPackagesAsync(
        string repoRoot,
        string? serviceDirectory,
        CancellationToken ct = default)
    {
        var packages = await GetPackageInfosFromMsBuildAsync(repoRoot, serviceDirectory ?? string.Empty, ct);

        // Populate CI parameters and triggering paths for each package
        foreach (var package in packages)
        {
            PopulateCiMetadata(package);
        }

        return packages;
    }

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(packagePath, ct);

        var parsed = await TryGetSinglePackageInfoFromMsBuildAsync(fullPath, ct);
        var package = parsed.HasValue
            ? await CreatePackageInfo(parsed.Value, repoRoot, relativePath, fullPath, ct)
            : await CreateBasicPackageInfo(repoRoot, relativePath, fullPath, ct);

        PopulateCiMetadata(package);

        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath} (as {sdkType})",
            package.PackageName ?? "(unknown)",
            package.PackageVersion ?? "(unknown)",
            relativePath,
            package.SdkType);

        return package;
    }

    private async Task<List<PackageInfo>> GetPackageInfosFromMsBuildAsync(
        string repoRoot,
        string serviceDirectory,
        CancellationToken ct)
    {
        var serviceProj = Path.Combine(repoRoot, "eng", "service.proj");
        var outputFilePath = Path.Combine(Path.GetTempPath(), $"package-info-{Guid.NewGuid()}.txt");

        try
        {
            var result = await RunMsBuildGetPackageInfoAsync(serviceProj, serviceDirectory, outputFilePath, repoRoot, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"MSBuild GetPackageInfo failed for {serviceProj}. Output: {result.Output}");
            }

            var lines = ReadAndDeleteOutputFile(outputFilePath);
            if (lines.Count == 0)
            {
                throw new InvalidOperationException($"MSBuild GetPackageInfo returned no packages for service directory '{serviceDirectory}'.");
            }

            return await ParsePackageInfoLines(lines, ct);
        }
        finally
        {
            // Ensure temp file is cleaned up
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }
        }
    }

    private async Task<ParsedMsBuildPackageInfo?> TryGetSinglePackageInfoFromMsBuildAsync(string packagePath, CancellationToken ct)
    {
        var srcDir = Path.Combine(packagePath, "src");
        if (!Directory.Exists(srcDir))
        {
            logger.LogDebug("No src directory found at {srcDir}, returning basic package info", srcDir);
            return null;
        }

        var csproj = Directory.GetFiles(srcDir, "*.csproj").FirstOrDefault();
        if (csproj == null)
        {
            logger.LogDebug("No .csproj file found in {srcDir}, returning basic package info", srcDir);
            return null;
        }

        logger.LogTrace("Getting package info via MSBuild for: {csproj}", csproj);

        var result = await processHelper.Run(new ProcessOptions(
            command: "dotnet",
            args: ["msbuild", csproj, "-getTargetResult:GetPackageInfo", "-nologo"]
        ), ct);

        if (result == null || result.ExitCode != 0)
        {
            logger.LogDebug("MSBuild GetPackageInfo failed for {csproj}. Output: {output}", csproj, result?.Output);
            return null;
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(result.Stdout);
            var identity = jsonDoc.RootElement
                .GetProperty("TargetResults")
                .GetProperty("GetPackageInfo")
                .GetProperty("Items")[0]
                .GetProperty("Identity")
                .GetString();

            return ParseMsBuildOutputLine(identity);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse MSBuild output for {csproj}", csproj);
            return null;
        }
    }

    private async Task<ProcessResult> RunMsBuildGetPackageInfoAsync(
        string serviceProj,
        string serviceDirectory,
        string outputFilePath,
        string repoRoot,
        CancellationToken ct)
    {
        var args = new[]
        {
            "msbuild",
            "/nologo",
            "/t:GetPackageInfo",
            serviceProj,
            $"/p:ServiceDirectory={serviceDirectory}",
            "/p:AddDevVersion=false",
            $"/p:OutputProjectInfoListFilePath={outputFilePath}",
            "-tl:off"
        };

        var result = await processHelper.Run(new ProcessOptions(
            command: "dotnet",
            args: args,
            workingDirectory: repoRoot
        ), ct);

        if (result == null)
        {
            var nullResult = new ProcessResult { ExitCode = -1 };
            nullResult.AppendStderr("Process returned null");
            return nullResult;
        }
        return result;
    }

    private static List<string> ReadAndDeleteOutputFile(string outputFilePath)
    {
        if (!File.Exists(outputFilePath))
        {
            return [];
        }

        var lines = File.ReadAllLines(outputFilePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        File.Delete(outputFilePath);
        return lines;
    }

    private async Task<List<PackageInfo>> ParsePackageInfoLines(List<string> lines, CancellationToken ct)
    {
        var packages = new List<PackageInfo>();

        foreach (var line in lines)
        {
            var parsed = ParseMsBuildOutputLine(line);
            if (parsed == null)
            {
                continue;
            }

            if (!Directory.Exists(parsed.Value.PackagePath))
            {
                logger.LogDebug("Skipping package with non-existent path: {path}", parsed.Value.PackagePath);
                continue;
            }

            var (repoRoot, relativePath, fullPath) = await packageInfoHelper.ParsePackagePathAsync(parsed.Value.PackagePath, ct);
            var pkg = await CreatePackageInfo(parsed.Value, repoRoot, relativePath, fullPath, ct);
            packages.Add(pkg);
        }

        return packages;
    }

    /// <summary>
    /// Parses the MSBuild output format: 'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder' 'AotCompatOptOut'
    /// </summary>
    private static ParsedMsBuildPackageInfo? ParseMsBuildOutputLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var parts = line.Split(MsBuildOutputSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\'', ' '))
            .ToArray();

        if (parts.Length < 6)
        {
            return null;
        }

        return new ParsedMsBuildPackageInfo(
            PackagePath: parts[0],
            ServiceDirectory: parts[1],
            PackageName: parts[2],
            PackageVersion: parts[3],
            SdkType: parts[4],
            IsNewSdk: bool.TryParse(parts[5], out var isNew) && isNew,
            AotCompatOptOut: parts.Length > 7 && bool.TryParse(parts[7], out var aot) ? aot : null
        );
    }

    private async Task<PackageInfo> CreatePackageInfo(ParsedMsBuildPackageInfo parsed, string repoRoot, string relativePath, string fullPath, CancellationToken ct)
    {
        // Build relative paths for DirectoryPath, ReadMePath, ChangeLogPath
        var directoryPath = $"sdk/{relativePath}";
        var readmePath = Path.Combine(fullPath, "README.md");
        var changelogPath = Path.Combine(fullPath, "CHANGELOG.md");

        var readmeRelative = File.Exists(readmePath) ? $"{directoryPath}/README.md" : string.Empty;
        var changelogRelative = File.Exists(changelogPath) ? $"{directoryPath}/CHANGELOG.md" : string.Empty;
        var releaseStatus = await changelogHelper.GetReleaseStatus(changelogPath, ct);

        return new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = parsed.PackageName,
            PackageVersion = parsed.PackageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.DotNet,
            SamplesDirectory = FindSamplesDirectory(fullPath),
            SdkTypeString = parsed.SdkType, // Use string directly - PackageInfo handles conversion
            ServiceDirectory = parsed.ServiceDirectory,
            ArtifactName = parsed.PackageName,
            IsNewSdk = parsed.IsNewSdk,
            AotCompatOptOut = parsed.AotCompatOptOut,
            DirectoryPath = directoryPath,
            ReadMePath = readmeRelative,
            ChangeLogPath = changelogRelative,
            ReleaseStatus = releaseStatus
        };
    }

    /// <summary>
    /// Creates a basic PackageInfo when MSBuild cannot provide package details.
    /// Used when the src directory or .csproj file is missing.
    /// </summary>
    private async Task<PackageInfo> CreateBasicPackageInfo(string repoRoot, string relativePath, string fullPath, CancellationToken ct)
    {
        // Build relative paths for DirectoryPath, ReadMePath, ChangeLogPath
        var directoryPath = $"sdk/{relativePath}";
        var readmePath = Path.Combine(fullPath, "README.md");
        var changelogPath = Path.Combine(fullPath, "CHANGELOG.md");

        var readmeRelative = File.Exists(readmePath) ? $"{directoryPath}/README.md" : string.Empty;
        var changelogRelative = File.Exists(changelogPath) ? $"{directoryPath}/CHANGELOG.md" : string.Empty;
        var releaseStatus = await changelogHelper.GetReleaseStatus(changelogPath, ct);

        return new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = null,
            PackageVersion = null,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = SdkLanguage.DotNet,
            SamplesDirectory = FindSamplesDirectory(fullPath),
            SdkType = SdkType.Unknown,
            DirectoryPath = directoryPath,
            ReadMePath = readmeRelative,
            ChangeLogPath = changelogRelative,
            ReleaseStatus = releaseStatus
        };
    }

    private readonly record struct ParsedMsBuildPackageInfo(
        string PackagePath,
        string ServiceDirectory,
        string PackageName,
        string PackageVersion,
        string SdkType,
        bool IsNewSdk,
        bool? AotCompatOptOut);

    private string FindSamplesDirectory(string packagePath)
    {
        try
        {
            var testsPath = Path.Combine(packagePath, "tests");
            if (!Directory.Exists(testsPath))
            {
                return GetDefaultSamplesDirectory(packagePath);
            }

            // Get all subdirectories under tests (sorted for consistent behavior across platforms)
            var testSubdirectories = Directory.GetDirectories(testsPath).OrderBy(d => d).ToArray();

            foreach (var directory in testSubdirectories)
            {
                var hasSamples = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                    .Any(file =>
                    {
                        try
                        {
                            return File.ReadAllText(file).Contains("#region Snippet:", StringComparison.Ordinal);
                        }
                        catch
                        {
                            return false;
                        }
                    });

                if (hasSamples)
                {
                    return directory;
                }
            }

            return GetDefaultSamplesDirectory(packagePath);
        }
        catch
        {
            return GetDefaultSamplesDirectory(packagePath);
        }
    }

    private static string GetDefaultSamplesDirectory(string packagePath)
        => Path.Combine(packagePath, "tests", "samples");

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var testsPath = Path.Combine(packagePath, "tests");
        var workingDirectory = Directory.Exists(testsPath) ? testsPath : packagePath;

        var result = await processHelper.Run(new ProcessOptions(
            command: "dotnet",
            args: ["test"],
            workingDirectory: workingDirectory
        ), ct);

        return new TestRunResponse(result);
    }

    public override bool HasCustomizations(string packagePath, CancellationToken ct)
    {
        try
        {
            var generatedDirMarker = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var csFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains(generatedDirMarker, StringComparison.OrdinalIgnoreCase));

            foreach (var file in csFiles)
            {
                try
                {
                    if (File.ReadLines(file).Any(line => line.Contains("partial class")))
                    {
                        logger.LogDebug("Found .NET partial class in {FilePath}", file);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for .NET customization files in {PackagePath}", packagePath);
            return false;
        }
    }
}
