// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Helpers;

/// <summary>
/// Selects packages affected by a PR diff based on changed files and triggering paths.
/// </summary>
public class PackageDiffSelector
{
    /// <summary>
    /// Information about changed files in a PR diff.
    /// </summary>
    public record DiffInfo(
        List<string> ChangedFiles,
        List<string> DeletedFiles,
        List<string> ExcludePaths);

    /// <summary>
    /// Selects packages affected by a diff.
    /// </summary>
    /// <param name="repoRoot">Absolute path to the repository root.</param>
    /// <param name="allPackages">All discovered packages.</param>
    /// <param name="diff">Information about changed/deleted files.</param>
    /// <returns>List of packages affected by the diff.</returns>
    public List<PackageInfo> SelectAffectedPackages(
        string repoRoot,
        IReadOnlyList<PackageInfo> allPackages,
        DiffInfo diff)
    {
        // Combine changed and deleted files
        var targetedFiles = BuildTargetedFileList(diff);
        if (targetedFiles.Count == 0)
        {
            return GetTemplatePackages(allPackages);
        }

        // Build lookup for additional validation packages
        var packageLookup = BuildPackageLookup(allPackages, repoRoot);
        
        // Find packages with direct changes or triggering path matches
        var affectedPackages = new List<PackageInfo>();
        var additionalPackagePaths = new List<string>();

        foreach (var package in allPackages)
        {
            if (IsPackageAffected(package, targetedFiles, repoRoot))
            {
                affectedPackages.Add(package);
                additionalPackagePaths.AddRange(package.AdditionalValidationPackages);
            }
        }

        // Add additional validation packages
        AddAdditionalValidationPackages(affectedPackages, additionalPackagePaths, packageLookup);

        // Fall back to template packages if nothing selected
        if (affectedPackages.Count == 0)
        {
            return GetTemplatePackages(allPackages);
        }

        return affectedPackages;
    }

    private static List<string> BuildTargetedFileList(DiffInfo diff)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var file in diff.ChangedFiles)
        {
            if (!IsExcluded(file, diff.ExcludePaths))
            {
                files.Add(NormalizePath(file));
            }
        }

        foreach (var file in diff.DeletedFiles)
        {
            if (!IsExcluded(file, diff.ExcludePaths))
            {
                files.Add(NormalizePath(file));
            }
        }

        return files.ToList();
    }

    private static bool IsExcluded(string file, List<string> excludePaths)
    {
        return excludePaths.Any(exclude => 
            file.StartsWith(exclude, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, PackageInfo> BuildPackageLookup(
        IReadOnlyList<PackageInfo> packages, 
        string repoRoot)
    {
        var lookup = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
        var normalizedRoot = NormalizePath(repoRoot);

        foreach (var package in packages)
        {
            // Key by directory path relative to repo root
            var key = package.DirectoryPath.TrimStart('/');
            lookup[key] = package;
        }

        return lookup;
    }

    private static bool IsPackageAffected(PackageInfo package, List<string> targetedFiles, string repoRoot)
    {
        var packageDir = NormalizePath(package.DirectoryPath).TrimStart('/');

        foreach (var file in targetedFiles)
        {
            var normalizedFile = file.TrimStart('/');

            // Check if file is under package directory
            if (IsUnderDirectory(normalizedFile, packageDir))
            {
                return true;
            }

            // Check triggering paths
            if (MatchesTriggeringPath(normalizedFile, package.TriggeringPaths, repoRoot))
            {
                package.IncludedForValidation = true;
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        // Exact match or file is inside the directory
        return string.Equals(filePath, directoryPath, StringComparison.OrdinalIgnoreCase) ||
               filePath.StartsWith($"{directoryPath}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTriggeringPath(string filePath, List<string> triggeringPaths, string repoRoot)
    {
        foreach (var triggerPath in triggeringPaths)
        {
            if (string.IsNullOrWhiteSpace(triggerPath))
            {
                continue;
            }

            // Triggering paths are stored with leading slash
            var normalizedTrigger = triggerPath.TrimStart('/');

            // Exact file match
            if (string.Equals(filePath, normalizedTrigger, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // File is under trigger directory
            if (IsUnderDirectory(filePath, normalizedTrigger))
            {
                return true;
            }

            // Trigger path is a file under a directory that was changed
            // (handles ci.yml changes triggering packages)
            if (IsUnderDirectory(normalizedTrigger, filePath))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddAdditionalValidationPackages(
        List<PackageInfo> affectedPackages,
        List<string> additionalPackagePaths,
        Dictionary<string, PackageInfo> packageLookup)
    {
        var existingPackages = new HashSet<string>(
            affectedPackages.Select(p => p.PackageName ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in additionalPackagePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var normalizedPath = NormalizePath(path).TrimStart('/');
            
            // Try lookup with and without sdk/ prefix
            var lookupKey = normalizedPath.StartsWith("sdk/", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : normalizedPath;

            if (packageLookup.TryGetValue(lookupKey, out var package) &&
                !existingPackages.Contains(package.PackageName ?? string.Empty))
            {
                package.IncludedForValidation = true;
                affectedPackages.Add(package);
                existingPackages.Add(package.PackageName ?? string.Empty);
            }
        }
    }

    private static List<PackageInfo> GetTemplatePackages(IReadOnlyList<PackageInfo> allPackages)
    {
        var templatePackages = new List<PackageInfo>();

        foreach (var package in allPackages)
        {
            var serviceDir = package.ServiceDirectory ?? string.Empty;
            if (string.Equals(serviceDir, "template", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(serviceDir, "template/aztemplate", StringComparison.OrdinalIgnoreCase))
            {
                package.IncludedForValidation = true;
                templatePackages.Add(package);
            }
        }

        return templatePackages;
    }

    private static string NormalizePath(string path) => path.Replace("\\", "/");
}
