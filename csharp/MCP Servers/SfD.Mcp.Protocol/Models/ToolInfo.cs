using System.Text.Json.Serialization;

namespace SfD.Mcp.Protocol.Models;

public class ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; set; }
}
