using System.Text.Json;

namespace ConfigWebService.Entities;

/// <summary>
/// Represents a configuration entry from the usr_svc_settings table.
/// </summary>
public class ConfigEntry
{
    public int Idx { get; set; }

    public string AppDomain { get; set; } = null!;

    public JsonDocument? Config { get; set; }

    /// <summary>
    /// Check if this entry is disabled (looks for "disabled": true in config JSONB)
    /// Defaults to false (enabled) if not specified
    /// </summary>
    public bool IsDisabled
    {
        get
        {
            if (Config is null)
                return false;
            
            if (Config.RootElement.TryGetProperty("disabled", out var disabledProp) &&
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
    /// Extracts a value from the config by property name (case-insensitive)
    /// </summary>
    /// <param name="propertyName">The JSON property name to extract</param>
    /// <returns>The JSON element if found, null otherwise</returns>
    public JsonElement? GetConfigValue(string propertyName)
    {
        if (Config is null)
            return null;

        // Case-insensitive property lookup
        foreach (var property in Config.RootElement.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }
}
