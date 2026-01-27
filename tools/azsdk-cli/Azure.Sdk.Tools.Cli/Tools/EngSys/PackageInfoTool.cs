// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Services.Languages;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[Description("Generate PackageInfo JSON files used by CI pipelines.")]
public class PackageInfoTool(
    IProcessHelper processHelper,
    IGitHelper gitHelper,
    ILogger<PackageInfoTool> _logger,
    IEnumerable<LanguageService> languageServices
) : LanguageMcpTool(languageServices, gitHelper, _logger)
{
    public override CommandGroup[] CommandHierarchy { get; set; } = [SharedCommandGroups.EngSys];

    private readonly Option<string> outDirOpt = new("--out-dir")
    {
        Description = "Output directory for PackageInfo JSON files",
        Required = true,
    };

    private readonly Option<string> serviceDirectoryOpt = new("--service-directory")
    {
        Description = "Service directory under sdk/ to scan (e.g. storage). Leave empty to scan all services.",
        Required = false,
    };

    private readonly Option<bool> ciOpt = new("--ci")
    {
        Description = "Select packages based on a CI PR diff (uses Azure Pipelines environment variables)",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string> repoRootOpt = new("--repo-root")
    {
        Description = "Path to the repository root. Defaults to the repo containing the working directory.",
        Required = false,
    };

    private readonly Option<bool> addDevVersionOpt = new("--add-dev-version")
    {
        Description = "Add DevVersion to PackageInfo output (used for daily builds).",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string[]> artifactListOpt = new("--artifact", "-a")
    {
        Description = "Artifact name(s) to filter the PackageInfo output (repeatable).",
        Required = false,
        AllowMultipleArgumentsPerToken = true,
    };

    protected override Command GetCommand() => new("package-info", "Generate PackageInfo JSON files for CI pipelines")
    {
        outDirOpt,
        serviceDirectoryOpt,
        ciOpt,
        repoRootOpt,
        addDevVersionOpt,
        artifactListOpt,
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        try
        {
            var options = NormalizeOptions(ParseOptions(parseResult));
            return await Execute(options, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate PackageInfo output.");
            return new DefaultCommandResponse { ResponseError = ex.Message };
        }
    }

    private PackageInfoOptions ParseOptions(ParseResult parseResult)
    {
        return new PackageInfoOptions(
            parseResult.GetValue(outDirOpt),
            parseResult.GetValue(serviceDirectoryOpt),
            parseResult.GetValue(ciOpt),
            parseResult.GetValue(repoRootOpt),
            parseResult.GetValue(addDevVersionOpt),
            parseResult.GetValue(artifactListOpt) ?? []);
    }

    private PackageInfoOptions NormalizeOptions(PackageInfoOptions options)
    {
        if (options.CiMode && !string.IsNullOrWhiteSpace(options.ServiceDirectory))
        {
            logger.LogWarning("Ignoring --service-directory because --ci is set.");
            return options with { ServiceDirectory = string.Empty };
        }

        return options;
    }

    private async Task<CommandResponse> Execute(PackageInfoOptions options, CancellationToken ct)
    {
        var repoRoot = ResolveRepoRoot(options.RepoRootOverride);
        var packageEntries = await GetPackageEntries(repoRoot, options.ServiceDirectory, ct);
        if (packageEntries.Count == 0)
        {
            return new DefaultCommandResponse { Message = "No packages found to process." };
        }

        var selectedPackages = await SelectPackages(repoRoot, packageEntries, options.CiMode, ct);
        if (selectedPackages.Count == 0)
        {
            return new DefaultCommandResponse { Message = "No packages matched the requested criteria." };
        }

        selectedPackages = FilterPackagesByArtifact(selectedPackages, options.ArtifactList);
        var outputFiles = WritePackageInfoFiles(selectedPackages, options.OutDir, options.AddDevVersion, repoRoot);

        return new DefaultCommandResponse
        {
            Message = $"Files written to {options.OutDir}:",
            Result = outputFiles
        };
    }

    private async Task<List<PackageEntry>> GetPackageEntries(
        string repoRoot,
        string? serviceDirectory,
        CancellationToken ct)
    {
        var packageProperties = await GetAllPackageProperties(repoRoot, serviceDirectory, ct);
        return packageProperties.Select(p => new PackageEntry(p)).ToList();
    }

    private async Task<List<PackageEntry>> SelectPackages(
        string repoRoot,
        List<PackageEntry> packageEntries,
        bool ciMode,
        CancellationToken ct)
    {
        if (!ciMode)
        {
            return packageEntries;
        }

        var diff = await BuildDiff(repoRoot, ct);
        return SelectPackagesForDiff(repoRoot, packageEntries, diff);
    }

    private List<PackageEntry> FilterPackagesByArtifact(List<PackageEntry> selectedPackages, string[] artifactList)
    {
        return PackageInfoArtifactFilter.FilterByArtifacts(
            selectedPackages.Select(p => p.Data).ToList(),
            artifactList,
            warning => logger.LogWarning("{warning}", warning))
            .Select(p => new PackageEntry(p))
            .ToList();
    }

    private List<string> WritePackageInfoFiles(
        List<PackageEntry> selectedPackages,
        string outDir,
        bool addDevVersion,
        string repoRoot)
    {
        var exportedPaths = new Dictionary<string, PackageEntry>(StringComparer.OrdinalIgnoreCase);
        var outputFiles = new List<string>();

        for (var i = 0; i < selectedPackages.Count; i++)
        {
            var pkg = selectedPackages[i];
            var packageInfoName = pkg.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packageInfoName))
            {
                continue;
            }

            var outputPath = Path.Combine(outDir, $"{packageInfoName}.json");
            if (exportedPaths.TryGetValue(outputPath, out var existing) && existing.IsNewSdk)
            {
                logger.LogInformation("Track 2 package info with file name {Path} already exported. Skipping export.", outputPath);
                continue;
            }

            LogPackageDetails(pkg, outputPath);

            exportedPaths[outputPath] = pkg;
            PackageInfoFileWriter.WritePackageInfoFile(pkg.Data, outputPath, addDevVersion, repoRoot);
            outputFiles.Add(outputPath);
        }

        return outputFiles;
    }

    private void LogPackageDetails(PackageEntry pkg, string outputPath)
    {
        logger.LogInformation("Package Name: {Name}", pkg.Name ?? "(unknown)");
        logger.LogInformation("Package Version: {Version}", pkg.Version ?? "(unknown)");
        logger.LogInformation("Package SDK Type: {SdkType}", pkg.SdkType ?? "(unknown)");
        logger.LogInformation("Artifact Name: {Artifact}", pkg.ArtifactName ?? "(unknown)");
        if (!string.IsNullOrEmpty(pkg.Group))
        {
            logger.LogInformation("GroupId: {Group}", pkg.Group);
        }
        if (!string.IsNullOrEmpty(pkg.SpecProjectPath))
        {
            logger.LogInformation("Spec Project Path: {SpecPath}", pkg.SpecProjectPath);
        }
        if (!string.IsNullOrEmpty(pkg.ReleaseStatus))
        {
            logger.LogInformation("Release date: {ReleaseStatus}", pkg.ReleaseStatus);
        }
        logger.LogInformation("Output path of json file: {OutputPath}", outputPath);
    }

    private string ResolveRepoRoot(string? repoRootOverride)
    {
        if (!string.IsNullOrEmpty(repoRootOverride))
        {
            return RealPath.GetRealPath(repoRootOverride);
        }

        return gitHelper.DiscoverRepoRoot(Environment.CurrentDirectory);
    }

    private async Task<List<JsonObject>> GetAllPackageProperties(string repoRoot, string? serviceDirectory, CancellationToken ct)
    {
        var languageService = GetLanguageService(repoRoot)
            ?? throw new InvalidOperationException("Unable to resolve language service for repository. Ensure repository name matches azure-sdk-for-<lang>.");

        var sdkRoot = Path.Combine(repoRoot, "sdk");
        var searchRoot = string.IsNullOrWhiteSpace(serviceDirectory)
            ? sdkRoot
            : Path.Combine(sdkRoot, serviceDirectory);

        if (!Directory.Exists(searchRoot))
        {
            throw new DirectoryNotFoundException($"Service directory does not exist: {searchRoot}");
        }

        var packageDirectories = GetPackageDirectories(languageService.Language, sdkRoot, searchRoot, !string.IsNullOrWhiteSpace(serviceDirectory));
        var packageInfos = new List<JsonObject>();

        foreach (var packageDirectory in packageDirectories)
        {
            var packageInfo = await languageService.GetPackageInfo(packageDirectory, ct);
            packageInfos.Add(BuildPackageInfoJson(packageInfo));
        }

        return packageInfos;
    }

    private async Task<PackageInfoDiff> BuildDiff(string repoRoot, CancellationToken ct)
    {
        var sourceCommitish = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCECOMMITID") ?? "HEAD";
        var targetBranchValue = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(targetBranchValue))
        {
            logger.LogWarning("SYSTEM_PULLREQUEST_TARGETBRANCH is not set. No diff will be calculated.");
            return new PackageInfoDiff([], [], [], [], Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER") ?? "-1");
        }

        var targetCommitish = NormalizeTargetBranch(targetBranchValue);
        var diffPath = NormalizeDiffPath(repoRoot, GetCiTargetPath(repoRoot));

        var changedFiles = await GetChangedFiles(repoRoot, targetCommitish, sourceCommitish, diffPath, "d", ct);
        var deletedFiles = await GetChangedFiles(repoRoot, targetCommitish, sourceCommitish, diffPath, "D", ct);

        var changedServices = GetChangedServices(changedFiles);
        var prNumber = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER") ?? "-1";

        return new PackageInfoDiff(
            ChangedFiles: changedFiles,
            ChangedServices: changedServices,
            ExcludePaths: [],
            DeletedFiles: deletedFiles,
            PrNumber: prNumber
        );
    }

    private List<PackageEntry> SelectPackagesForDiff(
        string repoRoot,
        List<PackageEntry> allPackages,
        PackageInfoDiff diff)
    {
        var targetedFiles = new List<string>(diff.ChangedFiles);
        if (diff.DeletedFiles.Count > 0)
        {
            targetedFiles.AddRange(diff.DeletedFiles);
        }

        var triggerPaths = GetTriggerPaths(allPackages);
        targetedFiles = UpdateTargetedFilesForExclude(targetedFiles, diff.ExcludePaths);
        targetedFiles = UpdateTargetedFilesForTriggerPaths(targetedFiles, triggerPaths);
        targetedFiles = targetedFiles
            .OrderByDescending(path => path.Split('/').Length)
            .ToList();

        var packagesWithChanges = new List<PackageEntry>();
        var additionalValidationPackages = new List<string>();
        var lookup = new Dictionary<string, PackageEntry>(StringComparer.OrdinalIgnoreCase);
        var directoryIndex = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var pkg in allPackages)
        {
            var pkgDirectory = NormalizePath(ResolveRepoPath(repoRoot, pkg.DirectoryPath));
            var lookupKey = pkgDirectory.Replace(NormalizePath(repoRoot), string.Empty).TrimStart('/', '\\');
            lookup[lookupKey] = pkg;

            foreach (var file in targetedFiles)
            {
                var filePath = NormalizePath(Path.Combine(repoRoot, file));
                var shouldInclude = string.Equals(filePath, pkgDirectory, StringComparison.OrdinalIgnoreCase) ||
                                    filePath.StartsWith($"{pkgDirectory}/", StringComparison.OrdinalIgnoreCase);

                if (!shouldInclude)
                {
                    foreach (var triggerPath in pkg.TriggeringPaths)
                    {
                        var resolved = NormalizePath(ResolveRepoPath(repoRoot, triggerPath));
                        var includedForValidation =
                            string.Equals(filePath, resolved, StringComparison.OrdinalIgnoreCase) ||
                            filePath.StartsWith($"{resolved}/", StringComparison.OrdinalIgnoreCase);

                        if (includedForValidation)
                        {
                            shouldInclude = true;
                            break;
                        }
                    }

                    if (!shouldInclude)
                    {
                        var triggeringCiYmls = pkg.TriggeringPaths
                            .Where(path => path.Contains("ci", StringComparison.OrdinalIgnoreCase) &&
                                           path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));

                        foreach (var yml in triggeringCiYmls)
                        {
                            var ciYml = ResolveRepoPath(repoRoot, yml);
                            var directory = NormalizePath(Path.GetDirectoryName(ciYml) ?? string.Empty);

                            if (!filePath.StartsWith($"{directory}/", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            var relative = filePath[(directory.Length + 1)..];
                            if (relative.Contains("/") || !Path.HasExtension(relative))
                            {
                                continue;
                            }

                            if (!directoryIndex.TryGetValue(directory, out var soleCiYml))
                            {
                                var directoryForSearch = directory.Replace("/", Path.DirectorySeparatorChar.ToString());
                                soleCiYml = Directory.Exists(directoryForSearch) &&
                                            Directory.GetFiles(directoryForSearch, "ci*.yml").Length == 1;
                                directoryIndex[directory] = soleCiYml;
                            }

                            if (soleCiYml && filePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                            {
                                shouldInclude = true;
                                break;
                            }
                        }
                    }
                }

                if (shouldInclude)
                {
                    packagesWithChanges.Add(pkg);
                    additionalValidationPackages.AddRange(pkg.AdditionalValidationPackages);
                    break;
                }
            }
        }

        var existingPackageNames = new HashSet<string>(
            packagesWithChanges.Select(pkg => pkg.Name ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        foreach (var addition in additionalValidationPackages)
        {
            if (string.IsNullOrWhiteSpace(addition))
            {
                continue;
            }

            var normalized = NormalizePath(addition);
            var key = Path.IsPathRooted(addition)
                ? normalized.Replace(NormalizePath(repoRoot), string.Empty).TrimStart('/', '\\')
                : normalized.TrimStart('/', '\\');

            if (lookup.TryGetValue(key, out var pkg) && !existingPackageNames.Contains(pkg.Name ?? string.Empty))
            {
                pkg.SetIncludedForValidation(true);
                packagesWithChanges.Add(pkg);
            }
        }

        if (packagesWithChanges.Count == 0)
        {
            foreach (var pkg in allPackages.Where(pkg =>
                         string.Equals(pkg.ServiceDirectory, "template", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(pkg.ServiceDirectory, "template/aztemplate", StringComparison.OrdinalIgnoreCase)))
            {
                pkg.SetIncludedForValidation(true);
                packagesWithChanges.Add(pkg);
            }
        }

        return packagesWithChanges;
    }

    private async Task<List<string>> GetChangedFiles(
        string repoRoot,
        string targetCommitish,
        string sourceCommitish,
        string? diffPath,
        string diffFilterType,
        CancellationToken ct)
    {
        var args = new List<string>
        {
            "-c",
            "core.quotepath=off",
            "-c",
            "i18n.logoutputencoding=utf-8",
            "diff",
            $"{targetCommitish}...{sourceCommitish}",
            "--name-only",
            $"--diff-filter={diffFilterType}"
        };

        if (!string.IsNullOrEmpty(diffPath))
        {
            args.Add("--");
            args.Add(diffPath);
        }

        var result = await processHelper.Run(new ProcessOptions("git", "git.exe", [.. args], logOutputStream: false, workingDirectory: repoRoot), ct);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"git diff failed: {result.Output}");
        }

        return result.Stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();
    }

    private static List<string> GetChangedServices(List<string> changedFiles)
    {
        var regex = new Regex(@"sdk/([^/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        return changedFiles
            .Select(path => path.Replace("\\", "/"))
            .Select(path =>
            {
                var match = regex.Match(path);
                return match.Success ? match.Groups[1].Value : null;
            })
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetTriggerPaths(IEnumerable<PackageEntry> packages)
    {
        var triggerPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pkg in packages)
        {
            foreach (var triggerPath in pkg.TriggeringPaths)
            {
                if (!string.IsNullOrEmpty(triggerPath) && Path.HasExtension(triggerPath))
                {
                    triggerPaths.Add(triggerPath);
                }
            }
        }

        return triggerPaths.ToList();
    }

    private static List<string> UpdateTargetedFilesForExclude(List<string> targetedFiles, List<string> excludePaths)
    {
        var results = new List<string>();
        foreach (var file in targetedFiles)
        {
            var shouldExclude = excludePaths.Any(exclude => file.StartsWith(exclude, StringComparison.CurrentCultureIgnoreCase));
            if (!shouldExclude)
            {
                results.Add(file);
            }
        }

        return results;
    }

    private static List<string> UpdateTargetedFilesForTriggerPaths(List<string> targetedFiles, List<string> triggerPaths)
    {
        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var triggers = new List<string>(triggerPaths);

        foreach (var file in targetedFiles)
        {
            var isExistingTriggerPath = false;
            var triggerIndex = -1;

            for (var i = 0; i < triggers.Count; i++)
            {
                var triggerPath = triggers[i];
                if (!string.IsNullOrEmpty(triggerPath) && string.Equals($"/{file}", triggerPath, StringComparison.OrdinalIgnoreCase))
                {
                    isExistingTriggerPath = true;
                    triggerIndex = i;
                    break;
                }
            }

            if (isExistingTriggerPath && triggerIndex >= 0)
            {
                triggers.RemoveAt(triggerIndex);
                processedFiles.Add(file);
                continue;
            }

            var normalized = file.Replace("/", Path.DirectorySeparatorChar.ToString());
            var directoryPath = Path.GetDirectoryName(normalized);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                processedFiles.Add(NormalizePath(directoryPath));
            }
            else
            {
                processedFiles.Add(file);
            }
        }

        return processedFiles.ToList();
    }

    private static string NormalizeTargetBranch(string targetBranch)
    {
        if (targetBranch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
        {
            return targetBranch;
        }

        if (targetBranch.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase))
        {
            return $"origin/{targetBranch["refs/heads/".Length..]}";
        }

        return $"origin/{targetBranch}";
    }

    private static string? NormalizeDiffPath(string repoRoot, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        var fullPath = Path.IsPathRooted(targetPath)
            ? targetPath
            : Path.Combine(repoRoot, targetPath);

        if (!fullPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return targetPath;
        }

        return Path.GetRelativePath(repoRoot, fullPath).Replace("\\", "/");
    }

    private static string ResolveRepoPath(string repoRoot, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return repoRoot;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path);
    }

    private static string NormalizePath(string path) => path.Replace("\\", "/");

    private static string GetCiTargetPath(string repoRoot)
    {
        var envPath = Environment.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY")
            ?? Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY");
        return string.IsNullOrWhiteSpace(envPath) ? repoRoot : envPath;
    }

    private static IEnumerable<string> GetPackageDirectories(
        SdkLanguage language,
        string sdkRoot,
        string searchRoot,
        bool isServiceRoot)
    {
        if (language == SdkLanguage.DotNet)
        {
            return GetDotNetPackageDirectories(sdkRoot, searchRoot, isServiceRoot);
        }

        var patterns = language switch
        {
            SdkLanguage.Java => new[] { "pom.xml" },
            SdkLanguage.JavaScript => new[] { "package.json" },
            SdkLanguage.Python => new[] { "setup.py", "pyproject.toml" },
            SdkLanguage.Go => new[] { "go.mod" },
            _ => Array.Empty<string>()
        };

        if (patterns.Length == 0)
        {
            return [];
        }

        var packageRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
        {
            foreach (var filePath in Directory.EnumerateFiles(searchRoot, pattern, SearchOption.AllDirectories))
            {
                var packageRoot = GetPackageRootFromManifest(language, filePath);
                if (!string.IsNullOrEmpty(packageRoot))
                {
                    packageRoots.Add(packageRoot);
                }
            }
        }

        return packageRoots;
    }

    private static IEnumerable<string> GetDotNetPackageDirectories(string sdkRoot, string searchRoot, bool isServiceRoot)
    {
        if (isServiceRoot)
        {
            return GetDotNetPackagesUnderService(searchRoot);
        }

        var packages = new List<string>();
        foreach (var serviceDir in Directory.GetDirectories(sdkRoot))
        {
            packages.AddRange(GetDotNetPackagesUnderService(serviceDir));
        }

        return packages;
    }

    private static IEnumerable<string> GetDotNetPackagesUnderService(string serviceRoot)
    {
        var packages = new List<string>();
        foreach (var packageDir in Directory.GetDirectories(serviceRoot))
        {
            var srcDir = Path.Combine(packageDir, "src");
            if (!Directory.Exists(srcDir))
            {
                continue;
            }

            if (Directory.GetFiles(srcDir, "*.csproj", SearchOption.TopDirectoryOnly).Length > 0)
            {
                packages.Add(packageDir);
            }
        }

        return packages;
    }

    private static string? GetPackageRootFromManifest(SdkLanguage language, string manifestPath)
    {
        var directory = Path.GetDirectoryName(manifestPath);
        if (string.IsNullOrEmpty(directory))
        {
            return null;
        }

        if (language == SdkLanguage.DotNet)
        {
            var directoryName = new DirectoryInfo(directory).Name;
            if (string.Equals(directoryName, "src", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(directoryName, "test", StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetParent(directory)?.FullName;
            }
        }

        return directory;
    }

    private static JsonObject BuildPackageInfoJson(PackageInfo info)
    {
        var repoRoot = info.RepoRoot;
        var packageRelativePath = Path.Combine("sdk", info.RelativePath).Replace("\\", "/");
        var readmePath = Path.Combine(info.PackagePath, "README.md");
        var changelogPath = Path.Combine(info.PackagePath, "CHANGELOG.md");
        var releaseStatus = info.ReleaseStatus;
        if (string.IsNullOrEmpty(releaseStatus) && File.Exists(changelogPath) && !string.IsNullOrEmpty(info.PackageVersion))
        {
            releaseStatus = GetReleaseStatus(changelogPath, info.PackageVersion) ?? string.Empty;
        }

        var serviceDirectory = !string.IsNullOrEmpty(info.ServiceDirectory)
            ? info.ServiceDirectory
            : GetServiceDirectoryFromRelativePath(info);

        return new JsonObject
        {
            ["Name"] = info.PackageName ?? string.Empty,
            ["ArtifactName"] = info.ArtifactName ?? info.PackageName ?? string.Empty,
            ["Version"] = info.PackageVersion ?? string.Empty,
            ["DirectoryPath"] = packageRelativePath,
            ["ServiceDirectory"] = serviceDirectory ?? string.Empty,
            ["ReadMePath"] = File.Exists(readmePath) ? Path.GetRelativePath(repoRoot, readmePath).Replace("\\", "/") : string.Empty,
            ["ChangeLogPath"] = File.Exists(changelogPath) ? Path.GetRelativePath(repoRoot, changelogPath).Replace("\\", "/") : string.Empty,
            ["Group"] = info.Group,
            ["SdkType"] = info.SdkType switch
            {
                SdkType.Management => "mgmt",
                SdkType.Dataplane => "client",
                SdkType.Functions => "functions",
                _ => string.Empty
            },
            ["IsNewSdk"] = info.IsNewSdk,
            ["ReleaseStatus"] = releaseStatus ?? string.Empty,
            ["IncludedForValidation"] = info.IncludedForValidation,
            ["AdditionalValidationPackages"] = null,
            ["ArtifactDetails"] = null,
            ["CIParameters"] = PackageInfoCiHelper.GetCiParameters(info),
            ["DevVersion"] = null
        };
    }

    private static string? GetReleaseStatus(string changelogPath, string version)
    {
        try
        {
            var regex = new Regex(
                @"^#+\s+(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z\.-]+)?)\s*(?<status>\([^)]+\))?",
                RegexOptions.Compiled);
            foreach (var line in File.ReadLines(changelogPath))
            {
                var match = regex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var matchedVersion = match.Groups["version"].Value;
                if (!string.Equals(matchedVersion, version, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var status = match.Groups["status"].Value;
                if (string.IsNullOrWhiteSpace(status))
                {
                    return string.Empty;
                }

                return status.Trim().Trim('(', ')');
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static string? GetTypeSpecProjectPathFromTspLocation(string tspLocationPath)
    {
        try
        {
            foreach (var line in File.ReadLines(tspLocationPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                {
                    continue;
                }

                if (trimmed.StartsWith("directory:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed["directory:".Length..].Trim().Trim('"', '\'');
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? GetServiceDirectoryFromRelativePath(PackageInfo info)
    {
        if (string.IsNullOrEmpty(info.RelativePath))
        {
            return null;
        }

        var segments = info.RelativePath.Replace("\\", "/").Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (info.Language == SdkLanguage.Go && segments.Length >= 2)
        {
            return $"{segments[0]}/{segments[1]}";
        }

        return segments[0];
    }

    private sealed record PackageInfoOptions(
        string OutDir,
        string? ServiceDirectory,
        bool CiMode,
        string? RepoRootOverride,
        bool AddDevVersion,
        string[] ArtifactList);

    private sealed class PackageEntry
    {
        public PackageEntry(JsonObject data)
        {
            Data = data;
        }

        public JsonObject Data { get; }

        public string? Name => Data["Name"]?.ToString();
        public string? Version => Data["Version"]?.ToString();
        public string? SdkType => Data["SdkType"]?.ToString();
        public string? ArtifactName => Data["ArtifactName"]?.ToString();
        public string? Group => Data["Group"]?.ToString();
        public string? SpecProjectPath => Data["SpecProjectPath"]?.ToString();
        public string? ReleaseStatus => Data["ReleaseStatus"]?.ToString();
        public string? DirectoryPath => Data["DirectoryPath"]?.ToString();
        public string? ServiceDirectory => Data["ServiceDirectory"]?.ToString();

        public bool IsNewSdk => bool.TryParse(Data["IsNewSdk"]?.ToString(), out var value) && value;

        public List<string> TriggeringPaths => [];
        public List<string> AdditionalValidationPackages => [];

        public void SetIncludedForValidation(bool included)
        {
            Data["IncludedForValidation"] = included;
        }
    }

    private record PackageInfoDiff(
        List<string> ChangedFiles,
        List<string> ChangedServices,
        List<string> ExcludePaths,
        List<string> DeletedFiles,
        [property: JsonPropertyName("PRNumber")] string PrNumber
    );
}
