using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OllamaMcpServer
{
    public class OllamaListResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = [];
    }
}
