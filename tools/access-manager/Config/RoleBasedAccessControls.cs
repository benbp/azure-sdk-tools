using System.Text;
using System.Text.Json.Serialization;

public class RoleBasedAccessControls
{
    [JsonRequired, JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonRequired, JsonPropertyName("scope")]
    public string? Scope { get; set; }


    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {Role}");
        sb.AppendLine($"Scope: {Scope}");
        return sb.ToString();
    }
}