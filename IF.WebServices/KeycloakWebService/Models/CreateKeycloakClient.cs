using System.Text.Json.Serialization;

public class CreateKeycloakClient
{
    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = default!;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "openid-connect";

    [JsonPropertyName("publicClient")]
    public bool PublicClient { get; set; }

    [JsonPropertyName("serviceAccountsEnabled")]
    public bool ServiceAccountsEnabled { get; set; }

    [JsonPropertyName("redirectUris")]
    public List<string>? RedirectUris { get; set; }

    [JsonPropertyName("webOrigins")]
    public List<string>? WebOrigins { get; set; }

    [JsonPropertyName("standardFlowEnabled")]
    public bool StandardFlowEnabled { get; set; } = true;

    [JsonPropertyName("directAccessGrantsEnabled")]
    public bool DirectAccessGrantsEnabled { get; set; } = false;
}