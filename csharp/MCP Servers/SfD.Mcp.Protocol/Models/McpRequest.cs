using System.Text.Json;
using System.Text.Json.Serialization;

namespace SfD.Mcp.Protocol.Models;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public McpParams? Params { get; set; }
}

public class McpParams
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}
