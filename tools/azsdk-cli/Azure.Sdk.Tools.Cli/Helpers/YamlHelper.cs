// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Sdk.Tools.Cli.Helpers;

internal static class YamlHelper
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Deserializes a YAML file into a strongly-typed model.
    /// Unknown properties are ignored.
    /// </summary>
    public static T? Deserialize<T>(string path) where T : class
    {
        try
        {
            using var reader = new StreamReader(path);
            return Deserializer.Deserialize<T>(reader);
        }
        catch
        {
            return null;
        }
    }
}
