using System.Text.Json;
using System.Text.Json.Serialization;

namespace IFGlobal.Configuration;

public enum AuthType
{
    User,
    Service,
    Registered
}

public class BootstrapConfig
{
    public string Realm { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string OpenIdConfig { get; set; } = string.Empty;
    public string LoggerService { get; set; } = string.Empty;

    [JsonConverter(typeof(FlexibleLogLevelConverter))]
    public string LogLevel { get; set; } = "Information";
}

/// <summary>
/// Handles LogLevel as either a string ("Information") or a number (2)
/// </summary>
public class FlexibleLogLevelConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "Information",
            JsonTokenType.Number => reader.GetInt32().ToString(),
            _ => "Information"
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public class SfdConfiguration
{
    public const string SectionName = "IF";

    public string ConfigService { get; set; } = "http://localhost:5000";
    
    /// <summary>
    /// Application domain identifier (e.g., 'Infoforum', 'BreakTackle')
    /// </summary>
    public string AppDomain { get; set; } = "Infoforum";
    
    /// <summary>
    /// Client secret for service authentication. 
    /// Set via appsettings.json or IF_CLIENTSECRET environment variable.
    /// Only required for Service AppType.
    /// </summary>
    public string? ClientSecret { get; set; }

    public AuthType AppType { get; set; } = AuthType.User;
}
