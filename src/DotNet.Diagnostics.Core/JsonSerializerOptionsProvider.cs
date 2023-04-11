using System.Text.Json;

namespace DotNet.Diagnostics.Core;

public sealed class JsonSerializerOptionsProvider
{
    public static JsonSerializerOptionsProvider Instance { get; } = new JsonSerializerOptionsProvider();
    private JsonSerializerOptionsProvider()
    {
        Default = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public JsonSerializerOptions Default { get; }
}