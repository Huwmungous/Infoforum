using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; } = "";
        [JsonPropertyName("data")]
        public object? Data { get; set; }
    }
}
