namespace IFGlobal.Configuration;

/// <summary>
/// Configuration options for the health controller.
/// </summary>
public class HealthControllerOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HealthController";

    /// <summary>
    /// The name of the service to include in health responses.
    /// If not set, the service name will not be included.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// The version of the service to include in health responses.
    /// If not set, the version will not be included.
    /// </summary>
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// Whether to include detailed health information.
    /// Default is false for security.
    /// </summary>
    public bool IncludeDetails { get; set; } = false;
}
