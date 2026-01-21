using Microsoft.AspNetCore.SignalR;

namespace LoggerWebService.Hubs;

/// <summary>
/// SignalR hub for real-time log streaming.
/// </summary>
public class LogHub : Hub
{
    private readonly ILogger<LogHub> _logger;

    public LogHub(ILogger<LogHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
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