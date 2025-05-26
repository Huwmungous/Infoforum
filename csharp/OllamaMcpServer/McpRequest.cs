using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class McpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        [JsonPropertyName("method")]
        public string Method { get; set; } = "";
        [JsonPropertyName("params")]
        public object? Params { get; set; }
        [JsonPropertyName("id")]
        public object? Id { get; set; }
    }
}
