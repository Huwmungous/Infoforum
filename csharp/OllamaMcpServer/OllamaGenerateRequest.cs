using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
        [JsonPropertyName("options")]
        public Dictionary<string, object>? Options { get; set; }
    }
}
