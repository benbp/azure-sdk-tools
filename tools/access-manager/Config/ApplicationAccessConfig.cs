using System.Text.Json.Serialization;

public class ApplicationAccessConfig
{
    [JsonRequired, JsonPropertyName("appDisplayName")]
    public string? AppDisplayName { get; set; }

    [JsonPropertyName("federatedIdentityCredentials")]
    public List<FederatedIdentityCredentialsConfig>? FederatedIdentityCredentials { get; set; }

    [JsonPropertyName("roleBasedAccessControls")]
    public List<RoleBasedAccessControls>? RoleBasedAccessControls { get; set; }
}