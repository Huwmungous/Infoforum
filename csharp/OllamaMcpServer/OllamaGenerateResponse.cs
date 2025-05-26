using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class OllamaGenerateResponse
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";
        [JsonPropertyName("done")]
        public bool Done { get; set; }
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
    }
}
