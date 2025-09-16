// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json.Serialization;

namespace Azure.Sdk.Tools.Cli.Models;

public class DefaultCommandResponse : CommandResponse
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public long Duration { get; set; }

    public override string ToString()
    {
        var output = new StringBuilder();
        if (!string.IsNullOrEmpty(Message))
        {
            output.AppendLine(Message);
        }
        if (Result != null)
        {
            output.AppendLine(Result?.ToString() ?? string.Empty);
        }
        if (Duration > 0)
        {
            output.AppendLine($"Duration: {Duration}ms");
        }

        return ToString(output);
    }

    public static implicit operator DefaultCommandResponse(string s) => new() { Message = s };
}
