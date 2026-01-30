using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace LoggerWebService.Hubs;

/// <summary>
/// SignalR hub for real-time log streaming.
/// </summary>
public class LogHub : Hub
{
    private readonly ILogger<LogHub> _logger;

    // Track minimum log level per connection
    private static readonly ConcurrentDictionary<string, int> ConnectionLogLevels = new();

    private static readonly Dictionary<string, int> LogLevelPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trace"] = 0,
        ["Debug"] = 1,
        ["Information"] = 2,
        ["Warning"] = 3,
        ["Error"] = 4,
        ["Critical"] = 5
    };

    public LogHub(ILogger<LogHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Default to showing all logs (Trace level = 0)
        ConnectionLogLevels[Context.ConnectionId] = 0;
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up connection's log level preference
        ConnectionLogLevels.TryRemove(Context.ConnectionId, out _);

        if(exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sets the minimum log level this client wants to receive.
    /// </summary>
    /// <param name="level">Minimum log level (Trace, Debug, Information, Warning, Error, Critical)</param>
    public void SetMinimumLogLevel(string level)
    {
        var priority = LogLevelPriority.GetValueOrDefault(level, 0);
        ConnectionLogLevels[Context.ConnectionId] = priority;
        _logger.LogInformation("Client {ConnectionId} set minimum log level to: {Level} (priority {Priority})",
            Context.ConnectionId, level, priority);
    }

    /// <summary>
    /// Gets connection IDs that should receive a log entry based on their level preferences.
    /// </summary>
    public static IEnumerable<string> GetEligibleConnections(string? logLevel)
    {
        var logPriority = LogLevelPriority.GetValueOrDefault(logLevel ?? "Information", 2);

        return ConnectionLogLevels
            .Where(kvp => logPriority >= kvp.Value)
            .Select(kvp => kvp.Key);
    }

    /// <summary>
    /// Allows clients to subscribe to logs from a specific realm.
    /// </summary>
    /// <param name="realm">The realm to subscribe to.</param>
    public async Task SubscribeToRealm(string realm)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"realm:{realm}");
        _logger.LogInformation("Client {ConnectionId} subscribed to realm: {Realm}", Context.ConnectionId, realm);
    }

    /// <summary>
    /// Allows clients to unsubscribe from a specific realm.
    /// </summary>
    /// <param name="realm">The realm to unsubscribe from.</param>
    public async Task UnsubscribeFromRealm(string realm)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"realm:{realm}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from realm: {Realm}", Context.ConnectionId, realm);
    }

    /// <summary>
    /// Allows clients to subscribe to logs from a specific client application.
    /// </summary>
    /// <param name="client">The client to subscribe to.</param>
    public async Task SubscribeToClient(string client)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"client:{client}");
        _logger.LogInformation("Client {ConnectionId} subscribed to client: {Client}", Context.ConnectionId, client);
    }

    /// <summary>
    /// Allows clients to unsubscribe from a specific client application.
    /// </summary>
    /// <param name="client">The client to unsubscribe from.</param>
    public async Task UnsubscribeFromClient(string client)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"client:{client}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from client: {Client}", Context.ConnectionId, client);
    }
}