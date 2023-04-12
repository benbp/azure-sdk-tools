using System.Text.Json.Serialization;

public class ApplicationAccessConfig
{
    [JsonRequired, JsonPropertyName("appDisplayName")]
    public string AppDisplayName { get; set; } = string.Empty;

    [JsonPropertyName("githubRepositorySecrets"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<GithubRepositorySecretsConfig> GithubRepositorySecrets { get; set; } = new List<GithubRepositorySecretsConfig>();

    [JsonPropertyName("federatedIdentityCredentials"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FederatedIdentityCredentialsConfig> FederatedIdentityCredentials { get; set; } = new List<FederatedIdentityCredentialsConfig>();

    [JsonPropertyName("roleBasedAccessControls"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RoleBasedAccessControlsConfig> RoleBasedAccessControls { get; set; } = new List<RoleBasedAccessControlsConfig>();
}