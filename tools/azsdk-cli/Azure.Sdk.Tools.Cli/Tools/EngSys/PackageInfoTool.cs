// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.Cli.Commands;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;
using Azure.Sdk.Tools.Cli.Tools.Core;

namespace Azure.Sdk.Tools.Cli.Tools.EngSys;

[Description("Generate PackageInfo JSON files used by CI pipelines.")]
public class PackageInfoTool(
    IPowershellHelper powershellHelper,
    IProcessHelper processHelper,
    IGitHelper gitHelper,
    ILogger<PackageInfoTool> logger
) : MCPTool
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

    private readonly Option<bool> fromDiffOpt = new("--from-diff")
    {
        Description = "Select packages based on a git diff (PR-style filtering)",
        Required = false,
        DefaultValueFactory = _ => false,
    };

    private readonly Option<string> repoRootOpt = new("--repo-root")
    {
        Description = "Path to the repository root. Defaults to the repo containing the working directory.",
        Required = false,
    };

    private readonly Option<string> targetPathOpt = new("--target-path")
    {
        Description = "Path under which changes will be detected when using --from-diff.",
        Required = false,
    };

    private readonly Option<string> sourceCommitOpt = new("--source-commit")
    {
        Description = "Source commit for diff (defaults to SYSTEM_PULLREQUEST_SOURCECOMMITID or HEAD).",
        Required = false,
    };

    private readonly Option<string> targetBranchOpt = new("--target-branch")
    {
        Description = "Target branch for diff (defaults to SYSTEM_PULLREQUEST_TARGETBRANCH or main).",
        Required = false,
    };

    private readonly Option<string[]> excludePathsOpt = new("--exclude-path")
    {
        Description = "Paths to exclude from diff targeting (repeatable).",
        Required = false,
        AllowMultipleArgumentsPerToken = true,
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
        fromDiffOpt,
        repoRootOpt,
        targetPathOpt,
        sourceCommitOpt,
        targetBranchOpt,
        excludePathsOpt,
        addDevVersionOpt,
        artifactListOpt,
    };

    public override async Task<CommandResponse> HandleCommand(ParseResult parseResult, CancellationToken ct)
    {
        var outDir = parseResult.GetValue(outDirOpt);
        var serviceDirectory = parseResult.GetValue(serviceDirectoryOpt);
        var fromDiff = parseResult.GetValue(fromDiffOpt);
        var repoRootOverride = parseResult.GetValue(repoRootOpt);
        var targetPath = parseResult.GetValue(targetPathOpt);
        var sourceCommit = parseResult.GetValue(sourceCommitOpt);
        var targetBranch = parseResult.GetValue(targetBranchOpt);
        var excludePaths = parseResult.GetValue(excludePathsOpt) ?? [];
        var addDevVersion = parseResult.GetValue(addDevVersionOpt);
        var artifactList = parseResult.GetValue(artifactListOpt) ?? [];

        try
        {
            if (fromDiff && !string.IsNullOrWhiteSpace(serviceDirectory))
            {
                logger.LogWarning("Ignoring --service-directory because --from-diff is set.");
            }

            var repoRoot = ResolveRepoRoot(repoRootOverride, targetPath);
            var packageProperties = await GetAllPackagePropertiesAsync(repoRoot, serviceDirectory, ct);
            if (packageProperties.Count == 0)
            {
                return new DefaultCommandResponse { Message = "No packages found to process." };
            }

            var packageEntries = packageProperties.Select(p => new PackageEntry(p)).ToList();
            List<PackageEntry> selectedPackages;
            PackageInfoDiff? diff = null;

            if (fromDiff)
            {
                diff = await BuildDiffAsync(repoRoot, targetPath, sourceCommit, targetBranch, excludePaths, ct);
                selectedPackages = await SelectPackagesForDiffAsync(repoRoot, packageEntries, diff, ct);
            }
            else
            {
                selectedPackages = packageEntries;
            }

            if (selectedPackages.Count == 0)
            {
                return new DefaultCommandResponse { Message = "No packages matched the requested criteria." };
            }

            selectedPackages = PackageInfoArtifactFilter.FilterByArtifacts(
                selectedPackages.Select(p => p.Data).ToList(),
                artifactList,
                warning => logger.LogWarning("{warning}", warning))
                .Select(p => new PackageEntry(p))
                .ToList();

            var packageInfoNames = await ResolvePackageInfoNamesAsync(repoRoot, selectedPackages, ct);

            var exportedPaths = new Dictionary<string, PackageEntry>(StringComparer.OrdinalIgnoreCase);
            var outputFiles = new List<string>();

            for (var i = 0; i < selectedPackages.Count; i++)
            {
                var pkg = selectedPackages[i];
                var packageInfoName = packageInfoNames[i];
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

                logger.LogInformation("Package Name: {Name}", pkg.Name ?? "(unknown)");
                logger.LogInformation("Package Version: {Version}", pkg.Version ?? "(unknown)");
                logger.LogInformation("Package SDK Type: {SdkType}", pkg.SdkType ?? "(unknown)");
                logger.LogInformation("Artifact Name: {Artifact}", pkg.ArtifactName ?? "(unknown)");
                if (!string.IsNullOrEmpty(pkg.Group))
                {
                    logger.LogInformation("GroupId: {Group}", pkg.Group);
                }
                logger.LogInformation("Spec Project Path: {SpecPath}", pkg.SpecProjectPath ?? "(unknown)");
                logger.LogInformation("Release date: {ReleaseStatus}", pkg.ReleaseStatus ?? "(unknown)");
                logger.LogInformation("Output path of json file: {OutputPath}", outputPath);

                exportedPaths[outputPath] = pkg;
                PackageInfoFileWriter.WritePackageInfoFile(pkg.Data, outputPath, addDevVersion, repoRoot);
                outputFiles.Add(outputPath);
            }

            return new DefaultCommandResponse
            {
                Message = $"Files written to {outDir}:",
                Result = outputFiles
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate PackageInfo output.");
            return new DefaultCommandResponse { ResponseError = ex.Message };
        }
    }

    private string ResolveRepoRoot(string? repoRootOverride, string? targetPath)
    {
        if (!string.IsNullOrEmpty(repoRootOverride))
        {
            return RealPath.GetRealPath(repoRootOverride);
        }

        var repoAnchor = !string.IsNullOrEmpty(targetPath) ? targetPath : Environment.CurrentDirectory;
        return gitHelper.DiscoverRepoRoot(repoAnchor);
    }

    private async Task<List<JsonObject>> GetAllPackagePropertiesAsync(string repoRoot, string? serviceDirectory, CancellationToken ct)
    {
        var commonPs1 = Path.Combine(repoRoot, "eng", "common", "scripts", "common.ps1");
        if (!File.Exists(commonPs1))
        {
            throw new FileNotFoundException($"Unable to find common.ps1 at {commonPs1}");
        }

        var serviceLiteral = string.IsNullOrWhiteSpace(serviceDirectory)
            ? "$null"
            : $"'{EscapePowerShell(serviceDirectory)}'";

        var script = $"""
            . '{EscapePowerShell(commonPs1)}'
            function LogInfo {{ }}
            function LogWarning {{ }}
            function LogError {{ param($message) Write-Error $message }}
            $serviceDirectory = {serviceLiteral}
            $packages = Get-AllPkgProperties -ServiceDirectory $serviceDirectory
            $packages | ConvertTo-Json -Depth 100
            """;

        var result = await powershellHelper.Run(new PowershellOptions([script], logOutputStream: false, workingDirectory: repoRoot), ct);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to load package properties: {result.Output}");
        }

        if (string.IsNullOrWhiteSpace(result.Stdout))
        {
            return [];
        }

        var node = JsonNode.Parse(result.Stdout);
        if (node is JsonArray array)
        {
            return array.OfType<JsonObject>().ToList();
        }

        if (node is JsonObject obj)
        {
            return [obj];
        }

        return [];
    }

    private async Task<PackageInfoDiff> BuildDiffAsync(
        string repoRoot,
        string? targetPath,
        string? sourceCommit,
        string? targetBranch,
        IEnumerable<string> excludePaths,
        CancellationToken ct)
    {
        var sourceCommitish = string.IsNullOrWhiteSpace(sourceCommit)
            ? Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCECOMMITID") ?? "HEAD"
            : sourceCommit;

        var targetBranchValue = string.IsNullOrWhiteSpace(targetBranch)
            ? Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_TARGETBRANCH") ?? "main"
            : targetBranch;

        var targetCommitish = NormalizeTargetBranch(targetBranchValue);
        var diffPath = NormalizeDiffPath(repoRoot, targetPath);

        var changedFiles = await GetChangedFilesAsync(repoRoot, targetCommitish, sourceCommitish, diffPath, "d", ct);
        var deletedFiles = await GetChangedFilesAsync(repoRoot, targetCommitish, sourceCommitish, diffPath, "D", ct);

        var changedServices = GetChangedServices(changedFiles);
        var prNumber = Environment.GetEnvironmentVariable("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER") ?? "-1";

        return new PackageInfoDiff(
            ChangedFiles: changedFiles,
            ChangedServices: changedServices,
            ExcludePaths: excludePaths.ToList(),
            DeletedFiles: deletedFiles,
            PrNumber: prNumber
        );
    }

    private async Task<List<PackageEntry>> SelectPackagesForDiffAsync(
        string repoRoot,
        List<PackageEntry> allPackages,
        PackageInfoDiff diff,
        CancellationToken ct)
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
                                break;
                            }

                            var relative = filePath[(directory.Length + 1)..];
                            if (relative.Contains("/") || !Path.HasExtension(relative))
                            {
                                break;
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

        var additionalFromFunction = await GetAdditionalValidationPackagesFromPackageSetAsync(repoRoot, packagesWithChanges, diff, allPackages, ct);
        if (additionalFromFunction.Count > 0)
        {
            packagesWithChanges.AddRange(additionalFromFunction);
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

    private async Task<List<string>> GetChangedFilesAsync(
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

    private async Task<List<PackageEntry>> GetAdditionalValidationPackagesFromPackageSetAsync(
        string repoRoot,
        List<PackageEntry> packagesWithChanges,
        PackageInfoDiff diff,
        List<PackageEntry> allPackages,
        CancellationToken ct)
    {
        var commonPs1 = Path.Combine(repoRoot, "eng", "common", "scripts", "common.ps1");
        if (!File.Exists(commonPs1))
        {
            return [];
        }

        var packagePath = Path.Combine(Path.GetTempPath(), $"packagesWithChanges-{Guid.NewGuid():N}.json");
        var diffPath = Path.Combine(Path.GetTempPath(), $"diff-{Guid.NewGuid():N}.json");
        var allPackagesPath = Path.Combine(Path.GetTempPath(), $"allPackages-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(packagePath, JsonSerializer.Serialize(packagesWithChanges.Select(p => p.Data), new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(diffPath, JsonSerializer.Serialize(diff, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(allPackagesPath, JsonSerializer.Serialize(allPackages.Select(p => p.Data), new JsonSerializerOptions { WriteIndented = true }));

            var script = $"""
                . '{EscapePowerShell(commonPs1)}'
                function LogInfo {{ }}
                function LogWarning {{ }}
                function LogError {{ param($message) Write-Error $message }}
                $packagesWithChanges = Get-Content -Raw '{EscapePowerShell(packagePath)}' | ConvertFrom-Json
                $diff = Get-Content -Raw '{EscapePowerShell(diffPath)}' | ConvertFrom-Json
                $allPackageProperties = Get-Content -Raw '{EscapePowerShell(allPackagesPath)}' | ConvertFrom-Json
                if ($AdditionalValidationPackagesFromPackageSetFn -and (Test-Path "Function:$AdditionalValidationPackagesFromPackageSetFn")) {{
                  $additional = & $AdditionalValidationPackagesFromPackageSetFn $packagesWithChanges $diff $allPackageProperties
                  $additional | ConvertTo-Json -Depth 100
                }}
                """;

            var result = await powershellHelper.Run(new PowershellOptions([script], logOutputStream: false, workingDirectory: repoRoot), ct);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
            {
                return [];
            }

            var node = JsonNode.Parse(result.Stdout);
            if (node is JsonArray array)
            {
                return array.OfType<JsonObject>().Select(obj => new PackageEntry(obj)).ToList();
            }

            if (node is JsonObject obj)
            {
                return [new PackageEntry(obj)];
            }

            return [];
        }
        finally
        {
            TryDeleteFile(packagePath);
            TryDeleteFile(diffPath);
            TryDeleteFile(allPackagesPath);
        }
    }

    private async Task<List<string>> ResolvePackageInfoNamesAsync(string repoRoot, List<PackageEntry> packages, CancellationToken ct)
    {
        if (packages.Count == 0)
        {
            return [];
        }

        var commonPs1 = Path.Combine(repoRoot, "eng", "common", "scripts", "common.ps1");
        if (!File.Exists(commonPs1))
        {
            return packages.Select(pkg => pkg.Name ?? string.Empty).ToList();
        }

        var packagesPath = Path.Combine(Path.GetTempPath(), $"package-info-names-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(packagesPath, JsonSerializer.Serialize(packages.Select(p => p.Data), new JsonSerializerOptions { WriteIndented = true }));

            var script = $"""
                . '{EscapePowerShell(commonPs1)}'
                function LogInfo {{ }}
                function LogWarning {{ }}
                function LogError {{ param($message) Write-Error $message }}
                $packages = Get-Content -Raw '{EscapePowerShell(packagesPath)}' | ConvertFrom-Json
                $results = @()
                foreach ($pkg in $packages) {{
                  $name = $pkg.Name
                  if (Test-Path "Function:Get-PackageInfoNameOverride") {{
                    $name = Get-PackageInfoNameOverride $pkg
                  }}
                  $results += $name
                }}
                $results | ConvertTo-Json -Depth 10
                """;

            var result = await powershellHelper.Run(new PowershellOptions([script], logOutputStream: false, workingDirectory: repoRoot), ct);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Stdout))
            {
                return packages.Select(pkg => pkg.Name ?? string.Empty).ToList();
            }

            var node = JsonNode.Parse(result.Stdout);
            if (node is JsonArray array)
            {
                var names = array.Select(value => value?.ToString() ?? string.Empty).ToList();
                return names.Count == packages.Count ? names : packages.Select(pkg => pkg.Name ?? string.Empty).ToList();
            }

            if (node is JsonValue value)
            {
                return packages.Count == 1 ? [value.ToString()] : packages.Select(pkg => pkg.Name ?? string.Empty).ToList();
            }

            return packages.Select(pkg => pkg.Name ?? string.Empty).ToList();
        }
        finally
        {
            TryDeleteFile(packagesPath);
        }
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

    private static string EscapePowerShell(string value) => value.Replace("'", "''");

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

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

        public List<string> TriggeringPaths
        {
            get
            {
                if (Data["ArtifactDetails"] is not JsonObject artifactDetails)
                {
                    return [];
                }

                if (artifactDetails["triggeringPaths"] is not JsonArray paths)
                {
                    return [];
                }

                return paths.Select(node => node?.ToString())
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToList()!;
            }
        }

        public List<string> AdditionalValidationPackages
        {
            get
            {
                if (Data["AdditionalValidationPackages"] is not JsonArray packages)
                {
                    return [];
                }

                return packages.Select(node => node?.ToString())
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToList()!;
            }
        }

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
