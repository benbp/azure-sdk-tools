using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Services;

public interface IRepositoryService
{
    Task<string> GetCommand(string commandName, string packagePath, CancellationToken ct);
    Task<bool> HasImplementation(string commandName, string packagePath, CancellationToken ct);
    Task<CLICheckResponse> Invoke(string commandName, string packagePath, OrderedDictionary args, CancellationToken ct);
}

public class RepoCommandContract
{
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;
}

public class RepositoryService(
    ILogger<RepositoryService> logger,
    IGitHelper gitHelper,
    IPowershellHelper powershellHelper
) : IRepositoryService
{
    private Dictionary<string, Dictionary<string, string>> repoCommandCache = [];

    private async Task Load(string packagePath, CancellationToken ct)
    {
        if (repoCommandCache.TryGetValue(packagePath, out var _))
        {
            return;
        }

        var repoRoot = gitHelper.DiscoverRepoRoot(packagePath);
        var contractPath = Path.Join(repoRoot, "eng", "azsdk-automation-contract.json");
        if (File.Exists(contractPath))
        {
            var json = await File.ReadAllTextAsync(contractPath, ct);
            var contract = JsonSerializer.Deserialize<List<RepoCommandContract>>(json);

            if (contract == null)
            {
                logger.LogWarning("Failed to deserialize contract at {ContractPath}", contractPath);
                return;
            }

            foreach (var command in contract)
            {
                foreach (var tag in command.Tags)
                {
                    repoCommandCache[packagePath][tag] = command.Command;
                }
            }
        }
    }

    public async Task<string> GetCommand(string commandName, string packagePath, CancellationToken ct)
    {
        await Load(packagePath, ct);

        if (!repoCommandCache.TryGetValue(packagePath, out var commandMap) ||
            !commandMap.TryGetValue(commandName, out string? value))
        {
            return null;
        }

        return value;
    }

    public async Task<bool> HasImplementation(string commandName, string packagePath, CancellationToken ct)
    {
        var command = await GetCommand(commandName, packagePath, ct);
        return command != null;
    }

    public async Task<CLICheckResponse> Invoke(string commandName, string packagePath, OrderedDictionary args, CancellationToken ct)
    {
        var scriptPath = await GetCommand(commandName, packagePath, ct);
        var paramJson = JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = false });
        var command = $"$params = ('{paramJson}' | ConvertFrom-Json -AsHashtable); & {scriptPath} @params";

        var options = new PowershellOptions(args: [command]);
        var result = await powershellHelper.Run(options, ct);

        return new CLICheckResponse(result.ExitCode, result.Output);
    }
}
