using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/*
 * EXAMPLE entry
 [
    {
        "appDisplayName": "azure-sdk-github-actions-test1",
        "roleBasedAccessControls": [
            {
              "role": "Contributor",
              "scope": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-testfoobaraccessmanager"
            },
            {
              "role": "Key Vault Secrets User",
              "scope": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-testfoobaraccessmanager/providers/Microsoft.KeyVault/vaults/testfoobaraccessmanager"
            }
        ],
        "federatedIdentityCredentials": [
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main tools",
              "issuer": "https://token.actions.githubusercontent.com",
              "name": "githubactionscredential-tools-main",
              "subject": "repo:azure/azure-sdk-tools:ref:refs/heads/main"
            },
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main net",
              "issuer": "https://token.actions.githubusercontent.com",
              "name": "githubactionscredential-net-main",
              "subject": "repo:azure/azure-sdk-for-net:ref:refs/heads/main"
            }
        ]
    }
]
*/

public class AccessConfig
{
    public string ConfigPath { get; set; } = default!;
    public List<ApplicationAccessConfig> ApplicationAccessConfigs { get; set; } = new List<ApplicationAccessConfig>();
    // Keep an unrendered version of config values so we can retain templating when we need to serialize back to the config file
    public List<ApplicationAccessConfig> RawApplicationAccessConfigs { get; set; } = new List<ApplicationAccessConfig>();

    public AccessConfig(string configPath)
    {
        ConfigPath = configPath;
        var contents = File.ReadAllText(ConfigPath);
        (ApplicationAccessConfigs, RawApplicationAccessConfigs) = AccessConfig.Initialize(contents);
    }

    public static (List<ApplicationAccessConfig>, List<ApplicationAccessConfig>) Initialize(string configText)
    {
        var rendered = new List<ApplicationAccessConfig>();
        var raw = new List<ApplicationAccessConfig>();

        var appAccessConfigs = JsonDocument.Parse(configText).RootElement.EnumerateArray();
        foreach (var element in appAccessConfigs)
        {
            var elementRendered = element.ToString();

            // Replace any {{ <key> }} strings in the config with the value from .properties.<key>
            if (JsonDocument.Parse(elementRendered).RootElement.TryGetProperty("properties", out var properties))
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    elementRendered = Regex.Replace(elementRendered, @"\{\{\s*" + prop.Name + @"\s*\}\}", prop.Value.ToString());
                }
            }

            rendered.Add(JsonSerializer.Deserialize<ApplicationAccessConfig>(elementRendered) ?? new ApplicationAccessConfig());
            raw.Add(JsonSerializer.Deserialize<ApplicationAccessConfig>(element.ToString()) ?? new ApplicationAccessConfig());
        }

        return (rendered, raw);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var app in ApplicationAccessConfigs!)
        {
            sb.AppendLine("---");
            sb.AppendLine($"AppDisplayName -> {app.AppDisplayName}");
            if (app.FederatedIdentityCredentials != null)
            {
                sb.AppendLine("FederatedIdentityCredentials ->");
                foreach (var cred in app.FederatedIdentityCredentials)
                {
                    sb.AppendLine(cred.ToIndentedString(1));
                }
            }
            if (app.RoleBasedAccessControls != null)
            {
                sb.AppendLine("RoleBasedAccessControls ->");
                foreach (var rbac in app.RoleBasedAccessControls)
                {
                    sb.AppendLine(rbac.ToIndentedString(1));
                }
            }
            if (app.GithubRepositorySecrets != null)
            {
                sb.AppendLine("GithubRepositorySecrets ->");
                foreach (var secret in app.GithubRepositorySecrets)
                {
                    sb.AppendLine(secret.ToIndentedString(1));
                }
            }
        }

        return sb.ToString();
    }
}