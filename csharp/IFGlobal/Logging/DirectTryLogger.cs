using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IFGlobal.Logging;

/// <summary>
/// Logger implementation that writes directly to the log database and notifies LoggerWebService.
/// Use this for services that cannot call LoggerWebService via HTTP (e.g., ConfigWebService).
/// </summary>
public class DirectTryLogger : ILogger
{
    private readonly string _categoryName;
    private readonly Func<DirectTryLogService> _logServiceFactory;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _applicationName;
    private readonly string _environmentName;
    private readonly LogLevel _minimumLogLevel;

    public DirectTryLogger(
        string categoryName,
        Func<DirectTryLogService> logServiceFactory,
        string realm,
        string clientId,
        string applicationName = "",
        string environmentName = "",
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        _categoryName = categoryName;
        _logServiceFactory = logServiceFactory;
        _realm = realm;
        _clientId = clientId;
        _applicationName = applicationName;
        _environmentName = environmentName;
        _minimumLogLevel = minimumLogLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLogLevel;

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

        // Always write to console (backup)
        Console.WriteLine($"[{logLevel}] {_categoryName}: {message}");

        // Write to database and notify SignalR asynchronously (fire and forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var logService = _logServiceFactory();

                var logData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    Timestamp = DateTime.UtcNow,
                    Level = logLevel.ToString(),
                    Category = _categoryName,
                    Message = message,
                    Exception = exception?.ToString(),
                    EventId = eventId.Id,
                    EventName = eventId.Name,
                    Application = _applicationName,
                    Environment = _environmentName,
                    Environment.MachineName
                }, LoggerJsonOptions.CamelCase));


                var request = new LogEntryRequest
                {
                    Realm = _realm,
                    Client = _clientId,
                    LogData = logData,
                    Environment = _environmentName,
                    Application = _applicationName,
                    LogLevel = logLevel.ToString()
                };

                await logService.LogAsync(request);
            }
            catch
            {
                // Silently fail - logging should never break the application
            }
        });
    }
}

/// <summary>
/// Logger provider for DirectTryLogger.
/// </summary>
public class DirectTryLoggerProvider : ILoggerProvider
{
    private readonly Func<DirectTryLogService> _logServiceFactory;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string _applicationName;
    private readonly string _environmentName;
    private readonly LogLevel _minimumLogLevel;

    public DirectTryLoggerProvider(
        Func<DirectTryLogService> logServiceFactory,
        string realm,
        string clientId,
        string applicationName = "",
        string environmentName = "",
        LogLevel minimumLogLevel = LogLevel.Information)
    {
        _logServiceFactory = logServiceFactory;
        _realm = realm;
        _clientId = clientId;
        _applicationName = applicationName;
        _environmentName = environmentName;
        _minimumLogLevel = minimumLogLevel;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DirectTryLogger(categoryName, _logServiceFactory, _realm, _clientId, _applicationName, _environmentName, _minimumLogLevel);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Configuration for DirectTryLogger.
/// </summary>
public class DirectTryLoggerConfiguration
{
    public required string Realm { get; set; }
    public required string ClientId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}
