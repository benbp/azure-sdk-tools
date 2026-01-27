// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text.Json;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Models.Responses.Package;

namespace Azure.Sdk.Tools.Cli.Services.Languages;

/// <summary>
/// Produces <see cref="PackageInfo"/> for .NET packages.
/// </summary>
public sealed partial class DotnetLanguageService: LanguageService
{
    private const string DotNetCommand = "dotnet";
    private const string RequiredDotNetVersion = "9.0.102"; // TODO - centralize this as part of env setup tool
    private const string GeneratedFolderName = "Generated";
    private static readonly TimeSpan CodeChecksTimeout = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan AotCompatTimeout = TimeSpan.FromMinutes(5);

    private readonly IPowershellHelper powershellHelper;

    public DotnetLanguageService(
        IProcessHelper processHelper,
        IPowershellHelper powershellHelper,
        IGitHelper gitHelper,
        ILogger<LanguageService> logger,
        ICommonValidationHelpers commonValidationHelpers,
        IFileHelper fileHelper,
        ISpecGenSdkConfigHelper specGenSdkConfigHelper,
        IChangelogHelper changelogHelper)
        : base(processHelper, gitHelper, logger, commonValidationHelpers, fileHelper, specGenSdkConfigHelper, changelogHelper)
    {
        this.powershellHelper = powershellHelper;
    }

    public override SdkLanguage Language { get; } = SdkLanguage.DotNet;
    public override bool IsCustomizedCodeUpdateSupported => true;

    private static readonly string[] separator = new[] { "' '" };

    /// <summary>
    /// Gets the default samples directory path relative to the package path.
    /// </summary>
    /// <param name="packagePath">The package path</param>
    /// <returns>The default samples directory path</returns>
    private static string GetDefaultSamplesDirectory(string packagePath) => Path.Combine(packagePath, "tests", "samples");

    public override async Task<PackageInfo> GetPackageInfo(string packagePath, CancellationToken ct = default)
    {
        logger.LogDebug("Resolving .NET package info for path: {packagePath}", packagePath);
        var (repoRoot, relativePath, fullPath) = await PackagePathParser.ParseAsync(gitHelper, packagePath, ct);
        var (packageName, packageVersion, sdkType, serviceDirectory, isNewSdk, aotCompatOptOut) = await TryGetPackageInfoAsync(fullPath, ct);

        if (string.IsNullOrWhiteSpace(packageName) ||
            string.IsNullOrWhiteSpace(packageVersion) ||
            string.IsNullOrWhiteSpace(sdkType) ||
            string.IsNullOrWhiteSpace(serviceDirectory))
        {
            throw new InvalidOperationException($"Failed to resolve .NET package info for {fullPath}.");
        }

        var samplesDirectory = FindSamplesDirectory(fullPath);

        var parsedSdkType = sdkType switch
        {
            "client" => SdkType.Dataplane,
            "mgmt" => SdkType.Management,
            "functions" => SdkType.Functions,
            _ => SdkType.Unknown
        };

        var model = new PackageInfo
        {
            PackagePath = fullPath,
            RepoRoot = repoRoot,
            RelativePath = relativePath,
            PackageName = packageName,
            PackageVersion = packageVersion,
            ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
            Language = Models.SdkLanguage.DotNet,
            SamplesDirectory = samplesDirectory,
            SdkType = parsedSdkType,
            ServiceDirectory = serviceDirectory,
            ArtifactName = packageName,
            IsNewSdk = isNewSdk,
            AotCompatOptOut = aotCompatOptOut
        };

        logger.LogDebug("Resolved .NET package: {packageName} v{packageVersion} at {relativePath} (as {parsedSdkType})",
            packageName ?? "(unknown)", packageVersion ?? "(unknown)", relativePath, parsedSdkType.ToString() ?? "(unknown)");

        return model;
    }

    public async Task<IReadOnlyList<PackageInfo>> GetPackageInfosForServiceDirectory(
        string repoRoot,
        string serviceDirectory,
        bool addDevVersion,
        CancellationToken ct = default)
    {
        var serviceProj = Path.Combine(repoRoot, "eng", "service.proj");
        var outputFilePath = Path.Combine(Path.GetTempPath(), $"package-info-{Guid.NewGuid()}.txt");

        var args = new List<string>
        {
            "msbuild",
            "/nologo",
            "/t:GetPackageInfo",
            serviceProj,
            $"/p:ServiceDirectory={serviceDirectory}",
            $"/p:AddDevVersion={addDevVersion}",
            $"/p:OutputProjectInfoListFilePath={outputFilePath}",
            "-tl:off"
        };

        var result = await processHelper.Run(new ProcessOptions(
            command: "dotnet",
            args: [.. args],
            workingDirectory: repoRoot
        ), ct);

        if (result == null || result.ExitCode != 0)
        {
            throw new InvalidOperationException($"MSBuild GetPackageInfo failed for {serviceProj}. Output: {result?.Output}");
        }

        var lines = new List<string>();
        if (File.Exists(outputFilePath))
        {
            lines = File.ReadAllLines(outputFilePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            File.Delete(outputFilePath);
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"MSBuild GetPackageInfo returned no packages for service directory '{serviceDirectory}'.");
        }

        var packageInfos = new List<PackageInfo>();
        foreach (var line in lines)
        {
            if (!TryParsePackageInfoLine(line, out var parsed))
            {
                continue;
            }

            var (repoRootResolved, relativePath, fullPath) = PackagePathParser.Parse(gitHelper, parsed.PackagePath);
            var samplesDirectory = FindSamplesDirectory(fullPath);
            var parsedSdkType = parsed.SdkType switch
            {
                "client" => SdkType.Dataplane,
                "mgmt" => SdkType.Management,
                "functions" => SdkType.Functions,
                _ => SdkType.Unknown
            };

            packageInfos.Add(new PackageInfo
            {
                PackagePath = fullPath,
                RepoRoot = repoRootResolved,
                RelativePath = relativePath,
                PackageName = parsed.PackageName,
                PackageVersion = parsed.PackageVersion,
                ServiceName = Path.GetFileName(Path.GetDirectoryName(fullPath)) ?? string.Empty,
                Language = Models.SdkLanguage.DotNet,
                SamplesDirectory = samplesDirectory,
                SdkType = parsedSdkType,
                ServiceDirectory = parsed.ServiceDirectory,
                ArtifactName = parsed.PackageName,
                IsNewSdk = parsed.IsNewSdk,
                AotCompatOptOut = parsed.AotCompatOptOut
            });
        }

        return packageInfos;
    }

    private async Task<(string Name, string Version, string SdkType, string ServiceDirectory, bool IsNewSdk, bool? AotCompatOptOut)> TryGetPackageInfoAsync(string packagePath, CancellationToken ct)
    {
        var csproj = Directory.GetFiles(Path.Combine(packagePath, "src"), "*.csproj").FirstOrDefault();

        if (csproj == null)
        {
            throw new InvalidOperationException($"No .csproj file found in {packagePath}.");
        }

        logger.LogTrace("Getting package info via MSBuild for: {csproj}", csproj);

        var result = await processHelper.Run(new ProcessOptions(
            command: "dotnet",
            args: ["msbuild", csproj, "-getTargetResult:GetPackageInfo", "-nologo"]
        ), ct);

        if (result == null || result.ExitCode != 0)
        {
            throw new InvalidOperationException($"MSBuild GetPackageInfo failed for {csproj}. Output: {result?.Output}");
        }

        // Parse JSON output
        using var jsonDoc = JsonDocument.Parse(result.Stdout);
        var targetResults = jsonDoc.RootElement.GetProperty("TargetResults");
        var getPackageInfo = targetResults.GetProperty("GetPackageInfo");
        var items = getPackageInfo.GetProperty("Items");
        if (items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"MSBuild GetPackageInfo returned no items for {csproj}.");
        }

        // Identity field which contains the package info
        var identity = items[0].GetProperty("Identity").GetString();

        // Parse the identity string:  'pkgPath' 'serviceDir' 'pkgName' 'pkgVersion' 'sdkType' 'isNewSdk' 'dllFolder' 'AotCompatOptOut'
        var parts = identity?.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\'', ' '))
            .ToArray();

        if (parts?.Length >= 6) // for now we only need items in the first 6 positions
        {
            var name = parts[2]; // pkgName
            var version = parts[3]; // pkgVersion
            var sdkType = parts[4]; // sdkType
            var serviceDirectory = parts[1]; // serviceDir
            var isNewSdk = bool.TryParse(parts[5], out var parsed) && parsed;
            bool? aotCompatOptOut = null;
            if (parts.Length > 7 && bool.TryParse(parts[7], out var parsedOptOut))
            {
                aotCompatOptOut = parsedOptOut;
            }

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(version) ||
                string.IsNullOrWhiteSpace(sdkType) ||
                string.IsNullOrWhiteSpace(serviceDirectory))
            {
                throw new InvalidOperationException($"MSBuild GetPackageInfo returned incomplete data for {csproj}.");
            }

            logger.LogTrace("Found package info via MSBuild: {name} v{version} ({sdkType})",
                name, version, sdkType);

            return (name, version, sdkType, serviceDirectory, isNewSdk, aotCompatOptOut);
        }

        throw new InvalidOperationException($"Unable to parse MSBuild GetPackageInfo identity for {csproj}.");
    }

    private static bool TryParsePackageInfoLine(string line, out ParsedPackageInfo parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\'', ' '))
            .ToArray();

        if (parts.Length < 6)
        {
            return false;
        }

        var packagePath = parts[0];
        if (!Directory.Exists(packagePath))
        {
            return false;
        }

        var serviceDirectory = parts[1];
        var packageName = parts[2];
        var packageVersion = parts[3];
        var sdkType = parts[4];
        var isNewSdk = bool.TryParse(parts[5], out var parsedIsNewSdk) && parsedIsNewSdk;
        bool? aotCompatOptOut = null;
        if (parts.Length > 7 && bool.TryParse(parts[7], out var parsedOptOut))
        {
            aotCompatOptOut = parsedOptOut;
        }

        parsed = new ParsedPackageInfo(
            packagePath,
            serviceDirectory,
            packageName,
            packageVersion,
            sdkType,
            isNewSdk,
            aotCompatOptOut);

        return true;
    }

    private readonly record struct ParsedPackageInfo(
        string PackagePath,
        string ServiceDirectory,
        string PackageName,
        string PackageVersion,
        string SdkType,
        bool IsNewSdk,
        bool? AotCompatOptOut);

    /// <summary>
    /// Finds the samples directory by looking for folders under tests that contain files with "#region Snippet:" in their content
    /// </summary>
    /// <param name="packagePath">The package path to search under</param>
    /// <returns>The path to the samples directory, or a default path if not found</returns>
    private string FindSamplesDirectory(string packagePath)
    {
        try
        {
            var testsPath = Path.Combine(packagePath, "tests");
            if (!Directory.Exists(testsPath))
            {
                logger.LogTrace("Tests directory not found at {testsPath}", testsPath);
                return GetDefaultSamplesDirectory(packagePath);
            }

            // Get all subdirectories under tests (sorted for consistent behavior across platforms)
            var testSubdirectories = Directory.GetDirectories(testsPath).OrderBy(d => d).ToArray();

            foreach (var directory in testSubdirectories)
            {
                // Look for .cs files containing "#region Snippet:" in their content
                var sampleFiles = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                    .Where(file =>
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            return content.Contains("#region Snippet:", StringComparison.Ordinal);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToArray();

                if (sampleFiles.Length > 0)
                {
                    logger.LogTrace("Found samples directory at {directory} with {count} files containing snippet regions",
                        directory, sampleFiles.Length);
                    return directory;
                }
            }

            logger.LogTrace("No samples directory found under {testsPath}, using default", testsPath);
            return GetDefaultSamplesDirectory(packagePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for samples directory under {packagePath}, using default", packagePath);
            return GetDefaultSamplesDirectory(packagePath);
        }
    }

    public override async Task<TestRunResponse> RunAllTests(string packagePath, CancellationToken ct = default)
    {
        var testsPath = Path.Combine(packagePath, "tests");
        var workingDirectory = Directory.Exists(testsPath) ? testsPath : packagePath;

        var result = await processHelper.Run(new ProcessOptions(
                command: "dotnet",
                args: ["test"],
                workingDirectory: workingDirectory
            ),
            ct
        );

        return new TestRunResponse(result);
    }

    public override bool HasCustomizations(string packagePath, CancellationToken ct)
    {
        // In azure-sdk-for-net, generated code lives in the Generated folder.
        // Customizations are partial types defined outside the Generated folder.
        // Example: sdk/ai/Azure.AI.DocumentIntelligence/src/
        //   - Generated/ (generated code)
        //   - Customized/ or other folders (customization code with partial classes)

        try
        {
            var generatedDirMarker = Path.DirectorySeparatorChar + GeneratedFolderName + Path.DirectorySeparatorChar;
            var csFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains(generatedDirMarker, StringComparison.OrdinalIgnoreCase));

            foreach (var file in csFiles)
            {
                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        if (line.Contains("partial class"))
                        {
                            logger.LogDebug("Found .NET partial class in {FilePath}", file);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read file {FilePath} for partial class detection", file);
                }
            }

            logger.LogDebug("No .NET partial classes found in {PackagePath}", packagePath);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching for .NET customization files in {PackagePath}", packagePath);
            return false;
        }
    }
}
