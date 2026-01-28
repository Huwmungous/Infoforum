using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;

namespace IFGlobal.Logging;

/// <summary>
/// JSON serializer options for camelCase property names (JavaScript-friendly).
/// </summary>
internal static class LoggerJsonOptions
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Logger implementation that sends log entries to:
/// - Console (always)
/// - Remote SfD logging service (configurable)
/// - Windows Event Log (on Windows, configurable)
/// - Log files (on Linux or when configured)
/// </summary>
public class IFLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _loggerServiceUrl;
    private readonly string _clientId;
    private readonly string _realm;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _httpClient;
    private readonly IFLoggerConfiguration? _config;
    private readonly string? _applicationName;
    private readonly string? _environmentName;

    private static readonly object FileLock = new();
    private static EventLog? _eventLog;
    private static bool _eventLogInitialised;
    private static readonly object EventLogLock = new();

    /// <summary>
    /// Creates a logger with explicit URL, clientId, and realm (legacy constructor).
    /// </summary>
    public IFLogger(
        string categoryName,
        string loggerServiceUrl,
        string clientId,
        string realm,
        string applicationName = "",
        string environmentName = "")
    {
        _categoryName = categoryName;
        _loggerServiceUrl = loggerServiceUrl;
        _clientId = clientId;
        _realm = realm;
        _applicationName = applicationName;
        _environmentName = environmentName;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Creates a logger with full configuration (for standalone/CLI applications).
    /// </summary>
    public IFLogger(
        string categoryName,
        IFLoggerConfiguration config)
    {
        _categoryName = categoryName;
        _config = config;
        _loggerServiceUrl = config.LoggerService;
        _clientId = config.ClientId;
        _realm = config.Realm;
        _applicationName = config.ApplicationName;
        _environmentName = config.EnvironmentName;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Creates a logger with IHttpClientFactory and configuration.
    /// </summary>
    public IFLogger(
        string categoryName,
        IHttpClientFactory httpClientFactory,
        IFLoggerConfiguration config,
        string realm,
        string clientId,
        string applicationName,
        string environmentName)
    {
        _categoryName = categoryName;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _realm = realm;
        _clientId = clientId;
        _applicationName = applicationName;
        _environmentName = environmentName;
        _loggerServiceUrl = config.LoggerService;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (_config != null)
            return logLevel >= _config.MinimumLogLevel;
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var fullMessage = exception != null
            ? $"{message}\n{exception}"
            : message;

        // 1. Always write to console
        WriteToConsole(logLevel, timestamp, fullMessage);

        // 2. Write to Windows Event Log (if enabled and on Windows)
        WriteToEventLog(logLevel, fullMessage);

        // 3. Write to file (if enabled, primarily for Linux)
        WriteToFile(logLevel, timestamp, fullMessage);

        // 4. Post to remote LoggerService asynchronously (fire and forget)
        PostToRemoteService(logLevel, message, exception);
    }

    private void WriteToConsole(LogLevel logLevel, string timestamp, string message)
    {
        var colour = logLevel switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };

        var originalColour = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = colour;
            Console.WriteLine($"[{timestamp}] [{logLevel,-11}] {_categoryName}: {message}");
        }
        finally
        {
            Console.ForegroundColor = originalColour;
        }
    }

    private void WriteToEventLog(LogLevel logLevel, string message)
    {
        // Only on Windows and if enabled
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var enableEventLog = _config?.EnableEventLog ?? false;
        if (!enableEventLog)
            return;

        try
        {
            EnsureEventLogInitialised();

            if (_eventLog == null)
                return;

            // Suppress CA1416 - we check for Windows platform at the start of this method
            #pragma warning disable CA1416
            var eventLogType = logLevel switch
            {
                LogLevel.Critical => EventLogEntryType.Error,
                LogLevel.Error => EventLogEntryType.Error,
                LogLevel.Warning => EventLogEntryType.Warning,
                _ => EventLogEntryType.Information
            };

            // Truncate message if too long for Event Log (max ~32KB)
            var truncatedMessage = message.Length > 30000
                ? message[..30000] + "... (truncated)"
                : message;

            _eventLog.WriteEntry($"[{_categoryName}] {truncatedMessage}", eventLogType);
            #pragma warning restore CA1416
        }
        catch
        {
            // Silently fail - logging should never break the application
        }
    }

    private void EnsureEventLogInitialised()
    {
        if (_eventLogInitialised)
            return;

        lock (EventLogLock)
        {
            if (_eventLogInitialised)
                return;

            try
            {
                var sourceName = _config?.EventLogSource ?? _applicationName ?? "SfD";
                var logName = _config?.EventLogName ?? "Application";

                // Suppress CA1416 - we check for Windows platform before calling this method
                #pragma warning disable CA1416
                // Try to create the event source if it doesn't exist
                // Note: This requires admin privileges on first run
                if (!EventLog.SourceExists(sourceName))
                {
                    try
                    {
                        EventLog.CreateEventSource(sourceName, logName);
                    }
                    catch (SecurityException)
                    {
                        // Can't create source without admin - use existing Application log
                        sourceName = "Application";
                    }
                }

                _eventLog = new EventLog(logName) { Source = sourceName };
                #pragma warning restore CA1416
            }
            catch
            {
                _eventLog = null;
            }
            finally
            {
                _eventLogInitialised = true;
            }
        }
    }

    private void WriteToFile(LogLevel logLevel, string timestamp, string message)
    {
        var enableFileLogging = _config?.EnableFileLogging ?? false;

        // Auto-enable file logging on Linux if not explicitly configured
        if (!enableFileLogging && _config == null && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            enableFileLogging = true;

        if (!enableFileLogging)
            return;

        try
        {
            var logDirectory = _config?.LogFilePath ?? GetDefaultLogDirectory();

            if (string.IsNullOrEmpty(logDirectory))
                return;

            // Ensure directory exists
            Directory.CreateDirectory(logDirectory);

            var appName = SanitiseFileName(_applicationName ?? "sfd");
            var logFileName = $"{appName}-{DateTime.Now:yyyy-MM-dd}.log";
            var logFilePath = Path.Combine(logDirectory, logFileName);

            var logEntry = $"[{timestamp}] [{logLevel,-11}] [{_categoryName}] {message}";

            lock (FileLock)
            {
                File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
            }
        }
        catch
        {
            // Silently fail - logging should never break the application
        }
    }

    private static string GetDefaultLogDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "/var/log/sfd";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SfD", "Logs");

        return Path.Combine(Environment.CurrentDirectory, "logs");
    }

    private static string SanitiseFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private void PostToRemoteService(LogLevel logLevel, string message, Exception? exception)
    {
        // Check if remote logging is enabled
        var enableRemote = _config?.EnableRemoteLogging ?? true;
        var serviceUrl = _config?.LoggerService ?? _loggerServiceUrl;

        if (!enableRemote || string.IsNullOrEmpty(serviceUrl))
            return;

        // Post to remote LoggerService asynchronously (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var request = new
                {
                    Realm = _realm,
                    Client = _clientId,
                    LogData = new
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = logLevel.ToString(),
                        Category = _categoryName,
                        Message = message,
                        Exception = exception?.ToString(),
                        ClientId = _clientId,
                        Application = _applicationName,
                        Environment = _environmentName,
                        MachineName = Environment.MachineName
                    },
                    Environment = _environmentName,
                    Application = _applicationName,
                    LogLevel = logLevel.ToString()
                };

                HttpClient client;
                if (_httpClientFactory != null)
                {
                    client = _httpClientFactory.CreateClient("IFLogger");
                }
                else if (_httpClient != null)
                {
                    client = _httpClient;
                }
                else
                {
                    return;
                }

                var content = new StringContent(
                    JsonSerializer.Serialize(request, LoggerJsonOptions.CamelCase),
                    Encoding.UTF8,
                    "application/json");

                await client.PostAsync(serviceUrl, content);
            }
            catch
            {
                // Silently fail - logging should never break the application
            }
        });
    }

    /// <summary>
    /// Logs an informational message (convenience method).
    /// </summary>
    public void LogInformation(string message)
    {
        Log(LogLevel.Information, default, message, null, (s, _) => s);
    }

    /// <summary>
    /// Logs a debug message (convenience method).
    /// </summary>
    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, default, message, null, (s, _) => s);
    }

    /// <summary>
    /// Logs a warning message (convenience method).
    /// </summary>
    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, default, message, null, (s, _) => s);
    }

    /// <summary>
    /// Logs an error message (convenience method).
    /// </summary>
    public void LogError(string message, Exception? exception = null)
    {
        Log(LogLevel.Error, default, message, exception, (s, _) => s);
    }

    /// <summary>
    /// Logs a critical message (convenience method).
    /// </summary>
    public void LogCritical(string message, Exception? exception = null)
    {
        Log(LogLevel.Critical, default, message, exception, (s, _) => s);
    }
}
