// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers.PackageInfoHelpers;

/// <summary>
/// Helper for extracting information from CHANGELOG.md files.
/// The changelog format is consistent across all Azure SDK languages.
/// </summary>
internal static class ChangelogHelper
{
    /// <summary>
    /// Extracts the release status (date or "Unreleased") from the first version entry in CHANGELOG.md.
    /// Format: ## &lt;version&gt; (&lt;date&gt;) or ## &lt;version&gt; (Unreleased)
    /// </summary>
    /// <param name="changelogPath">Absolute path to the CHANGELOG.md file.</param>
    /// <returns>The release status string (e.g., "2022-04-26" or "Unreleased"), or empty string if not found.</returns>
    public static string GetReleaseStatus(string changelogPath)
    {
        if (!File.Exists(changelogPath))
        {
            return string.Empty;
        }

        try
        {
            foreach (var line in File.ReadLines(changelogPath))
            {
                // Match lines like: ## 1.0.3-beta.20 (2022-04-26) or ## 1.0.0 (Unreleased)
                if (!line.StartsWith("## ", StringComparison.Ordinal))
                {
                    continue;
                }

                var openParen = line.IndexOf('(');
                var closeParen = line.IndexOf(')');
                if (openParen < 0 || closeParen < openParen)
                {
                    continue;
                }

                var status = line.Substring(openParen + 1, closeParen - openParen - 1).Trim();
                return status;
            }
        }
        catch
        {
            // Ignore errors reading changelog
        }

        return string.Empty;
    }
}
