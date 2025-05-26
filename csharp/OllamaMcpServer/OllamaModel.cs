using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("modified_at")]
        public string ModifiedAt { get; set; } = "";
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("digest")]
        public string Digest { get; set; } = "";
    }
}
