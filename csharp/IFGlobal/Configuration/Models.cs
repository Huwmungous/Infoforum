using System.Text.Json;
using System.Text.Json.Serialization;

namespace IFGlobal.Configuration;

public enum AuthType
{
    User,
    Service,
    Registered, // for future use
    Patient
}

public class BootstrapConfig
{
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
    public const string SectionName = "SfD";

    public string ConfigService { get; set; } = "http://localhost:5000/config";
    public string Realm { get; set; } = "SfdDevelopment_Dev";
    public string Client { get; set; } = "dev-login";

    public string ClientSecret { get; set; } = string.Empty;
    public AuthType AppType { get; set; } = AuthType.User;
}
