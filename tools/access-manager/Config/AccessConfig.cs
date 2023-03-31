using System.Text;
using System.Text.Json;

/*
 * EXAMPLE entry
 [
    {
        "appDisplayName": "azure-sdk-github-actions-test1",
        "roleBasedAccessControls": [],
        "federatedIdentityCredentials": [
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main tools",
              "id": "6bfce5de-931a-405c-b2a9-23d74e1e81fc",
              "issuer": "https://token.actions.githubusercontent.com",
              "name": "githubactionscredential-tools-main",
              "subject": "repo:azure/azure-sdk-tools:ref:refs/heads/main"
            },
            {
              "audiences": [
                "api://azureadtokenexchange"
              ],
              "description": "event processor oidc main net",
              "id": "90ac4b15-5fb3-4645-8213-2712b4fcd95d",
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
    public string ConfigPath { get; set; }

    public List<ApplicationAccessConfig>? ApplicationAccessConfigs { get; set; }

    public AccessConfig(string configPath)
    {
        ConfigPath = configPath;
    }

    public void Initialize()
    {
        var contents = File.ReadAllText(ConfigPath);
        ApplicationAccessConfigs = JsonSerializer.Deserialize<List<ApplicationAccessConfig>>(contents);
        Console.WriteLine("");
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
        }

        return sb.ToString();
    }
}