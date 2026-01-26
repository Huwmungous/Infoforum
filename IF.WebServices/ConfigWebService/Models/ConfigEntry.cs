using System.Text.Json;

namespace ConfigWebService.Entities;

/// <summary>
/// Represents a configuration entry from the usr_svc_settings table.
/// </summary>
public class ConfigEntry
{
    public int Idx { get; set; }

    public string AppDomain { get; set; } = null!;

    public JsonDocument? UserConfig { get; set; }

    public JsonDocument? ServiceConfig { get; set; }

    public JsonDocument? BootstrapConfig { get; set; }

    /// <summary>
    /// Check if this entry is disabled (looks for "disabled": true in bootstrap_config JSONB)
    /// Defaults to false (enabled) if not specified
    /// </summary>
    public bool IsDisabled
    {
        get
        {
            if (BootstrapConfig is null)
                return false;
            
            if (BootstrapConfig.RootElement.TryGetProperty("disabled", out var disabledProp) &&
                disabledProp.ValueKind == JsonValueKind.True)
                return true;
            
            return false;
        }
    }

    /// <summary>
    /// Check if this entry is enabled (inverse of IsDisabled)
    /// </summary>
    public bool IsEnabled => !IsDisabled;

    /// <summary>
    /// Extracts a value from the specified config column by JSON path
    /// </summary>
    /// <param name="configType">"user" or "service"</param>
    /// <param name="propertyName">The JSON property name to extract (case-insensitive)</param>
    /// <returns>The JSON element if found, null otherwise</returns>
    public JsonElement? GetConfigValue(string configType, string propertyName)
    {
        var config = configType.ToLowerInvariant() switch
        {
            "user" => UserConfig,
            "service" => ServiceConfig,
            _ => null
        };

        if (config is null)
            return null;

        // Case-insensitive property lookup
        foreach (var property in config.RootElement.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }
}
