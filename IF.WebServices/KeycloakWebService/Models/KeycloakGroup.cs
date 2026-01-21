using System.Text.Json.Serialization;

namespace KeycloakWebService.Models
{
    /// <summary>
    /// Represents a Keycloak group
    /// </summary>
    public class KeycloakGroup
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("attributes")]
        public Dictionary<string, List<string>>? Attributes { get; set; }

        [JsonPropertyName("members")]
        public List<KeycloakUser>? Members { get; set; }
    }

}