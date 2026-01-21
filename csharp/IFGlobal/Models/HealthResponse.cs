using System.Text.Json.Serialization;

namespace IFGlobal.Models;

/// <summary>
/// Standard health check response.
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Health status: "healthy", "degraded", or "unhealthy".
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "healthy";

    /// <summary>
    /// UTC timestamp of the health check.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional service name.
    /// </summary>
    [JsonPropertyName("service")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Service { get; set; }

    /// <summary>
    /// Optional service version.
    /// </summary>
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    /// <summary>
    /// Optional additional details about component health.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Details { get; set; }
}
