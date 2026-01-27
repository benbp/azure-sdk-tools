// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Azure.Sdk.Tools.Cli.Helpers;

internal static class YamlHelper
{
    public static YamlMappingNode? LoadYamlMapping(string path)
    {
        using var reader = new StreamReader(path);
        var yaml = new YamlStream();
        yaml.Load(new Parser(reader));
        return yaml.Documents.Count > 0 ? yaml.Documents[0].RootNode as YamlMappingNode : null;
    }

    public static YamlNode? TryGetPath(YamlMappingNode root, params string[] path)
    {
        YamlNode? current = root;
        foreach (var key in path)
        {
            if (current is not YamlMappingNode map)
            {
                return null;
            }

            if (!TryGetChild(map, key, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    public static string? TryGetScalar(YamlMappingNode root, params string[] path)
    {
        var node = TryGetPath(root, path);
        return (node as YamlScalarNode)?.Value;
    }

    public static JsonNode? ConvertToJson(YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                return ConvertScalar(scalar.Value);
            case YamlSequenceNode sequence:
                var array = new JsonArray();
                foreach (var child in sequence.Children)
                {
                    array.Add(child == null ? null : ConvertToJson(child));
                }
                return array;
            case YamlMappingNode mapping:
                var obj = new JsonObject();
                foreach (var entry in mapping.Children)
                {
                    var key = (entry.Key as YamlScalarNode)?.Value ?? string.Empty;
                    obj[key] = entry.Value == null ? null : ConvertToJson(entry.Value);
                }
                return obj;
            default:
                return null;
        }
    }

    public static bool TryGetChild(YamlMappingNode map, string key, out YamlNode value)
    {
        foreach (var kvp in map.Children)
        {
            if (kvp.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    private static JsonNode? ConvertScalar(string? value)
    {
        if (value == null)
        {
            return null;
        }

        if (bool.TryParse(value, out var parsedBool))
        {
            return JsonValue.Create(parsedBool);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
        {
            return JsonValue.Create(parsedLong);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
        {
            return JsonValue.Create(parsedDouble);
        }

        return JsonValue.Create(value);
    }
}
