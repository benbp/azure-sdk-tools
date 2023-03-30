using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Graph.Models;

public class FederatedIdentityCredentialsConfig
{
    [JsonRequired, JsonPropertyName("audiences")]
    public List<string>? Audiences { get; set; }

    [JsonRequired, JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonRequired, JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonRequired, JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    [JsonRequired, JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonRequired, JsonPropertyName("subject")]
    public string? Subject { get; set; }

    public FederatedIdentityCredential ToFederatedIdentityCredential()
    {
        return new FederatedIdentityCredential
        {
            Name = Name,
            Description = Description,
            Issuer = Issuer,
            Subject = Subject,
            Audiences = Audiences
        };
    }

    public string ToIndentedString(int indentLevel)
    {
        var indent = "";
        foreach (var lvl in Enumerable.Range(0, indentLevel))
        {
            indent += "    ";
        }

        var sb = new StringBuilder();
        sb.AppendLine(indent + $"Audiences: {string.Join(", ", Audiences!)}");
        sb.AppendLine(indent + $"Description: {Description}");
        sb.AppendLine(indent + $"Id: {Id}");
        sb.AppendLine(indent + $"Issuer: {Issuer}");
        sb.AppendLine(indent + $"Name: {Name}");
        sb.AppendLine(indent + $"Subject: {Subject}");

        return sb.ToString();
    }
}