using Microsoft.Extensions.Logging;

namespace IFGlobal.Logging;

/// <summary>
/// Configuration for the IF logging service.
/// Supports console, remote service, Windows Event Log, and file logging.
/// </summary>
public class IFLoggerConfiguration
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "IFLogger";

    /// <summary>
    /// The URL of the remote logging service.
    /// </summary>
    public string LoggerService { get; set; } = string.Empty;

    /// <summary>
    /// Alternative property name for backwards compatibility.
    /// </summary>
    public string LoggerServiceUrl
    {
        get => LoggerService;
        set => LoggerService = value;
    }

    /// <summary>
    /// The realm for logging context.
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// The client ID for logging context.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Whether remote logging is enabled.
    /// </summary>
    public bool EnableRemoteLogging { get; set; } = true;

    /// <summary>
    /// The minimum log level to send to the loggers.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// The application/service name for identifying the log source.
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// The environment name (e.g., Development, Staging, Production).
    /// </summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to log to Windows Event Log (Windows only).
    /// </summary>
    public bool EnableEventLog { get; set; } = false;

    /// <summary>
    /// The Event Log source name (defaults to ApplicationName).
    /// </summary>
    public string EventLogSource { get; set; } = string.Empty;

    /// <summary>
    /// The Event Log name (defaults to "Application").
    /// </summary>
    public string EventLogName { get; set; } = "Application";

    /// <summary>
    /// Whether to log to files.
    /// Auto-enabled on Linux if not explicitly set.
    /// </summary>
    public bool EnableFileLogging { get; set; } = false;

    /// <summary>
    /// The directory path for log files.
    /// Defaults to /var/log/if on Linux, %ProgramData%\IF\Logs on Windows.
    /// </summary>
    public string LogFilePath { get; set; } = string.Empty;
}

/// <summary>
/// Backwards compatibility alias for IFLoggerConfiguration.
/// </summary>
[Obsolete("Use IFLoggerConfiguration instead")]
public class SfdLoggerConfiguration : IFLoggerConfiguration { }
