using Microsoft.Extensions.Logging;
using IFGlobal.Auth;
using System.Net.Http.Json;
using System.Text.Json;

namespace IFGlobal.Logging;

/// <summary>
/// Service for direct logging - writes directly to the log database
/// and notifies LoggerWebService for SignalR broadcast.
/// Use this when the calling service cannot use the normal logging path
/// (e.g., ConfigWebService logging to avoid circular dependency).
/// </summary>
public class DirectTryLogService
{
    private readonly LogEntryService _logEntryService;
    private readonly HttpClient _httpClient;
    private readonly string _openIdConfig;
    private readonly string _serviceClientId;
    private readonly string? _loggerServiceUrl;
    private readonly ILogger<DirectTryLogService> _logger;

    public DirectTryLogService(
        LogEntryService logEntryService,
        HttpClient httpClient,
        string openIdConfig,
        string serviceClientId,
        string? loggerServiceUrl,
        ILogger<DirectTryLogService> logger)
    {
        _logEntryService = logEntryService;
        _httpClient = httpClient;
        _openIdConfig = openIdConfig;
        _serviceClientId = serviceClientId;
        _loggerServiceUrl = loggerServiceUrl;
        _logger = logger;
    }

    /// <summary>
    /// Log an entry directly to the database and notify SignalR clients.
    /// </summary>
    /// <param name="realm">The realm/tenant identifier.</param>
    /// <param name="client">The client application identifier.</param>
    /// <param name="logData">The log data as JSON.</param>
    /// <returns>The created log entry index.</returns>
    public async Task<int> LogAsync(LogEntryRequest logEntry)
    {
        // Write directly to database
        var idx = await _logEntryService.AddLogEntryAsync(logEntry);
        var createdAt = DateTime.UtcNow;

        // Notify LoggerWebService for SignalR broadcast
        if (string.IsNullOrEmpty(_loggerServiceUrl))
        {
            _logger.LogDebug("LoggerService URL not configured, skipping SignalR notification");
            return idx;
        }

        try
        {
            var accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync(true, _openIdConfig, _serviceClientId);
            var logEntryResponse = new LogEntryResponse(idx, logEntry.Realm, logEntry.Client, logEntry.LogData, createdAt);

            var notifyUrl = $"{_loggerServiceUrl.TrimEnd('/')}/notify";

            using var request = new HttpRequestMessage(HttpMethod.Post, notifyUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(logEntryResponse);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to notify LoggerWebService for SignalR broadcast. Status: {StatusCode}",
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - the DB write succeeded
            _logger.LogWarning(ex, "Failed to notify LoggerWebService for SignalR broadcast");
        }

        return idx;
    }
}
