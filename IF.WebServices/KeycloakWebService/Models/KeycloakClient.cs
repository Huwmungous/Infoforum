using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeycloakWebService.Models
{
    public class KeycloakClient
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }               // internal UUID

        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; }         // human-readable ID

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("protocol")]
        public string? Protocol { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("redirectUris")]
        public List<string>? RedirectUris { get; set; }
    }
}
