using System.Collections.ObjectModel;
using ChitterChatterClient.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ChitterChatterClient.Services;

/// <summary>
/// Service for managing SignalR connection to ChitterChatter hub.
/// </summary>
public sealed class ChatterHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private readonly Func<Task<string?>>? _accessTokenProvider;
    private long _audioSequenceNumber;

    public event Action<ConnectionState>? ConnectionStateChanged;
    public event Action<IReadOnlyList<ChatterRoom>, IReadOnlyList<ChatterUser>>? InitialStateReceived;
    public event Action<UserStatusUpdate>? UserConnected;
    public event Action<UserStatusUpdate>? UserDisconnected;
    public event Action<UserStatusUpdate>? UserStatusChanged;
    public event Action<string, string, string>? UserJoinedRoom;  // userId, username, roomId
    public event Action<string, string, string>? UserLeftRoom;    // userId, username, roomId
    public event Action<PrivateCallRequest>? IncomingCall;
    public event Action<string, string>? CallAccepted;  // callGroupId, participants json
    public event Action<string>? CallDeclined;
    public event Action<string, string>? CallEnded;  // endedByUserId, reason
    public event Action<AudioPacket>? AudioReceived;
    public event Action<SpeakingStatus>? SpeakingStatusChanged;
    public event Action<string, bool>? UserMuteChanged;
    public event Action<string>? ErrorReceived;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public string? CurrentUserId { get; private set; }
    public string? CurrentUsername { get; private set; }

    public ChatterHubService(string hubUrl, Func<Task<string?>>? accessTokenProvider = null)
    {
        _hubUrl = hubUrl;
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task ConnectAsync(string? userId = null, string? username = null)
    {
        if (_connection is not null)
        {
            await DisconnectAsync();
        }

        CurrentUserId = userId;
        CurrentUsername = username;

        var builder = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                if (_accessTokenProvider is not null)
                {
                    options.AccessTokenProvider = _accessTokenProvider;
                }
            })
            .AddMessagePackProtocol()
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) });

        _connection = builder.Build();

        RegisterHandlers();

        _connection.Closed += async (error) =>
        {
            UpdateState(ConnectionState.Disconnected);
            if (error is not null)
            {
                ErrorReceived?.Invoke($"Connection closed: {error.Message}");
            }
            await Task.CompletedTask;
        };

        _connection.Reconnecting += async (error) =>
        {
            UpdateState(ConnectionState.Reconnecting);
            await Task.CompletedTask;
        };

        _connection.Reconnected += async (connectionId) =>
        {
            UpdateState(ConnectionState.Connected);
            await Task.CompletedTask;
        };

        try
        {
            UpdateState(ConnectionState.Connecting);
            await _connection.StartAsync();
            UpdateState(ConnectionState.Connected);
        }
        catch (Exception ex)
        {
            UpdateState(ConnectionState.Failed);
            ErrorReceived?.Invoke($"Connection failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
            UpdateState(ConnectionState.Disconnected);
        }
    }

    private void RegisterHandlers()
    {
        if (_connection is null) return;

        _connection.On<object>("InitialState", (state) =>
        {
            // Parse the dynamic object - in real implementation, deserialise properly
            // For now, this triggers the event for the ViewModel to request data
            InitialStateReceived?.Invoke([], []);
        });

        _connection.On<UserStatusUpdate>("UserConnected", (update) =>
        {
            UserConnected?.Invoke(update);
        });

        _connection.On<UserStatusUpdate>("UserDisconnected", (update) =>
        {
            UserDisconnected?.Invoke(update);
        });

        _connection.On<UserStatusUpdate>("UserStatusChanged", (update) =>
        {
            UserStatusChanged?.Invoke(update);
        });

        _connection.On<object>("UserJoinedRoom", (data) =>
        {
            // Extract properties from dynamic object
            var dict = data as IDictionary<string, object>;
            if (dict is not null)
            {
                var userId = dict["UserId"]?.ToString() ?? "";
                var username = dict["Username"]?.ToString() ?? "";
                var roomId = dict["RoomId"]?.ToString() ?? "";
                UserJoinedRoom?.Invoke(userId, username, roomId);
            }
        });

        _connection.On<object>("UserLeftRoom", (data) =>
        {
            var dict = data as IDictionary<string, object>;
            if (dict is not null)
            {
                var userId = dict["UserId"]?.ToString() ?? "";
                var username = dict["Username"]?.ToString() ?? "";
                var roomId = dict["RoomId"]?.ToString() ?? "";
                UserLeftRoom?.Invoke(userId, username, roomId);
            }
        });

        _connection.On<PrivateCallRequest>("IncomingCall", (request) =>
        {
            IncomingCall?.Invoke(request);
        });

        _connection.On<object>("CallAccepted", (data) =>
        {
            var dict = data as IDictionary<string, object>;
            if (dict is not null)
            {
                var callGroupId = dict["CallGroupId"]?.ToString() ?? "";
                var participants = dict["Participants"]?.ToString() ?? "[]";
                CallAccepted?.Invoke(callGroupId, participants);
            }
        });

        _connection.On<object>("CallDeclined", (data) =>
        {
            var dict = data as IDictionary<string, object>;
            var reason = dict?["Reason"]?.ToString() ?? "Declined";
            CallDeclined?.Invoke(reason);
        });

        _connection.On<object>("CallEnded", (data) =>
        {
            var dict = data as IDictionary<string, object>;
            if (dict is not null)
            {
                var endedByUserId = dict["EndedByUserId"]?.ToString() ?? "";
                var reason = dict["Reason"]?.ToString() ?? "";
                CallEnded?.Invoke(endedByUserId, reason);
            }
        });

        _connection.On<AudioPacket>("ReceiveAudio", (packet) =>
        {
            AudioReceived?.Invoke(packet);
        });

        _connection.On<SpeakingStatus>("SpeakingStatusChanged", (status) =>
        {
            SpeakingStatusChanged?.Invoke(status);
        });

        _connection.On<object>("UserMuteChanged", (data) =>
        {
            var dict = data as IDictionary<string, object>;
            if (dict is not null)
            {
                var userId = dict["UserId"]?.ToString() ?? "";
                var isMuted = dict["IsMuted"] is bool m && m;
                UserMuteChanged?.Invoke(userId, isMuted);
            }
        });

        _connection.On<string>("Error", (message) =>
        {
            ErrorReceived?.Invoke(message);
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ROOM OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<ChatterRoom>> GetRoomsAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<IReadOnlyList<ChatterRoom>>("GetRooms");
    }

    public async Task JoinRoomAsync(string roomId)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("JoinRoom", roomId);
    }

    public async Task LeaveRoomAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("LeaveRoom");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE CALLS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task CallUserAsync(string targetUserId)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("CallUser", targetUserId);
    }

    public async Task AcceptCallAsync(string fromUserId)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("AcceptCall", fromUserId);
    }

    public async Task DeclineCallAsync(string fromUserId)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("DeclineCall", fromUserId);
    }

    public async Task EndCallAsync()
    {
        EnsureConnected();
        await _connection!.InvokeAsync("EndCall");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDIO STREAMING
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task SendAudioAsync(byte[] audioData)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            return;
        }

        var sequenceNumber = Interlocked.Increment(ref _audioSequenceNumber);
        await _connection.SendAsync("SendAudio", audioData, sequenceNumber);
    }

    public async Task StartSpeakingAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.SendAsync("StartSpeaking");
        }
    }

    public async Task StopSpeakingAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.SendAsync("StopSpeaking");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // USER STATUS
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task SetMutedAsync(bool isMuted)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SetMuted", isMuted);
    }

    public async Task SetDeafenedAsync(bool isDeafened)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("SetDeafened", isDeafened);
    }

    public async Task<IReadOnlyList<UserStatusUpdate>> GetOnlineUsersAsync()
    {
        EnsureConnected();
        return await _connection!.InvokeAsync<IReadOnlyList<UserStatusUpdate>>("GetOnlineUsers");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private void EnsureConnected()
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("Not connected to hub");
        }
    }

    private void UpdateState(ConnectionState newState)
    {
        State = newState;
        ConnectionStateChanged?.Invoke(newState);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
