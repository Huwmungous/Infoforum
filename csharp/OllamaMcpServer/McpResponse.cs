using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class McpResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
        [JsonPropertyName("result")]
        public object? Result { get; set; }
        [JsonPropertyName("error")]
        public object? Error { get; set; }
        [JsonPropertyName("id")]
        public object? Id { get; set; }
    }
}
