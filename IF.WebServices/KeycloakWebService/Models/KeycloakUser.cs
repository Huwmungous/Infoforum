namespace KeycloakWebService.Models
{

    /// <summary>
    /// Represents a Keycloak user
    /// </summary>
    public class KeycloakUser
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("username")]
        public string? Username { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string? Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }

}