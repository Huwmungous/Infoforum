using System.Text.Json;
using System.Text.Json.Serialization;

namespace IFOllama.WebService.Models;

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public McpError? Error { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ToolDefinition
{
    public string ServerName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonElement InputSchema { get; set; }
}

public record ToolCallRequest(string ServerName, string ToolName, JsonElement Arguments);

public record ChatRequest(string Message, List<OllamaMessage>? History, string? Model);
