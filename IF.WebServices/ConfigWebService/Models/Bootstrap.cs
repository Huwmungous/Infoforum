using System.Text.Json.Serialization;

namespace ConfigWebService.Models;

/// <summary>
/// Reference model for the expected bootstrap_config JSON schema.
/// This class is provided for documentation and serialisation purposes.
/// The actual configuration is stored as JSONB in the database.
/// </summary>
/// <example>
/// Example bootstrap_config JSON:
/// {
///   "realm": "SfdDevelopment_Dev",
///   "userClientId": "dev-login-usr",
///   "serviceClientId": "dev-login-svc",
///   "serviceClientSecret": "your-secret-here",
///   "openIdConfig": "https://sfddevelopment.com/auth/realms",
///   "loggerService": "https://sfddevelopment.com/logger",
///   "logLevel": "Debug",
///   "allowedScopes": ["openid", "profile", "email"],
///   "requiresRelay": false
/// }
/// </example>
public class Bootstrap
{
    [JsonPropertyName("realm")]
    public string Realm { get; set; } = "";

    [JsonPropertyName("userClientId")]
    public string UserClientId { get; set; } = "";

    [JsonPropertyName("serviceClientId")]
    public string ServiceClientId { get; set; } = "";

    [JsonPropertyName("serviceClientSecret")]
    public string? ServiceClientSecret { get; set; }

    [JsonPropertyName("openIdConfig")]
    public string OpenIdConfig { get; set; } = "";

    [JsonPropertyName("loggerService")]
    public string LoggerService { get; set; } = "";

    [JsonPropertyName("logLevel")]
    public string LogLevel { get; set; } = "Information";

    [JsonPropertyName("allowedScopes")]
    public string[] AllowedScopes { get; set; } = ["openid", "profile", "email"];

    [JsonPropertyName("requiresRelay")]
    public bool RequiresRelay { get; set; }
}
