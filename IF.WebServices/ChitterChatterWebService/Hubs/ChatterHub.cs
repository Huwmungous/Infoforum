using System.Security.Claims;
using ChitterChatterWebService.Models;
using ChitterChatterWebService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChitterChatterWebService.Hubs;

/// <summary>
/// SignalR hub for ChitterChatter voice communication.
/// Handles presence, room management, private calls, and audio streaming.
/// </summary>
[Authorize]
public sealed class ChatterHub : Hub
{
    private readonly ChatterStateService _stateService;
    private readonly ILogger<ChatterHub> _logger;

    public ChatterHub(ChatterStateService stateService, ILogger<ChatterHub> logger)
    {
        _stateService = stateService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CONNECTION LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
        {
            _logger.LogWarning("Connection rejected: missing user identity");
            Context.Abort();
            return;
        }

        var user = _stateService.AddUser(userId, username, Context.ConnectionId);
        if (user is null)
        {
            _logger.LogWarning("Failed to add user {Username} to state", username);
            Context.Abort();
            return;
        }

        // Notify all clients of the new user
        await Clients.Others.SendAsync("UserConnected", new UserStatusUpdate
        {
            UserId = userId,
            Username = username,
            Status = UserStatus.Online,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });

        // Send current state to the connecting user
        await SendCurrentState();

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = _stateService.RemoveUserByConnectionId(Context.ConnectionId);

        if (user is not null)
        {
            // Notify others that user disconnected
            await Clients.Others.SendAsync("UserDisconnected", new UserStatusUpdate
            {
                UserId = user.UserId,
                Username = user.Username,
                Status = UserStatus.Offline
            });

            // If they were in a private call, notify the other party
            if (user.PrivateCallWithUserId is not null)
            {
                var otherUser = _stateService.GetUserByUserId(user.PrivateCallWithUserId);
                if (otherUser is not null)
                {
                    await Clients.Client(otherUser.ConnectionId).SendAsync("PrivateCallEnded", new
                    {
                        EndedByUserId = user.UserId,
                        Reason = "Disconnected"
                    });
                }
            }

            // If they were in a room, notify room members
            if (user.CurrentRoomId is not null)
            {
                await Clients.Group(user.CurrentRoomId).SendAsync("UserLeftRoom", new
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    RoomId = user.CurrentRoomId
                });
            }
        }

        if (exception is not null)
        {
            _logger.LogError(exception, "Client disconnected with error");
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ROOM OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all available rooms.
    /// </summary>
    public Task<IReadOnlyList<ChatterRoom>> GetRooms()
    {
        return Task.FromResult(_stateService.GetAllRooms());
    }

    /// <summary>
    /// Join a voice chat room.
    /// </summary>
    public async Task JoinRoom(string roomId)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null)
        {
            await Clients.Caller.SendAsync("Error", "User not found");
            return;
        }

        var previousRoomId = user.CurrentRoomId;

        if (!_stateService.JoinRoom(userId, roomId))
        {
            await Clients.Caller.SendAsync("Error", "Failed to join room");
            return;
        }

        // Leave previous SignalR group
        if (previousRoomId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previousRoomId);
            await Clients.Group(previousRoomId).SendAsync("UserLeftRoom", new
            {
                UserId = userId,
                Username = user.Username,
                RoomId = previousRoomId
            });
        }

        // Join new SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        // Notify room members
        await Clients.Group(roomId).SendAsync("UserJoinedRoom", new
        {
            UserId = userId,
            Username = user.Username,
            RoomId = roomId,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });

        // Send room participants to the joining user
        var participants = _stateService.GetUsersInRoom(roomId);
        await Clients.Caller.SendAsync("RoomJoined", new
        {
            RoomId = roomId,
            Participants = participants.Select(p => new
            {
                p.UserId,
                p.Username,
                p.IsMuted,
                p.IsDeafened
            })
        });

        // Broadcast user status change
        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = UserStatus.InRoom,
            RoomId = roomId,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });
    }

    /// <summary>
    /// Leave the current room.
    /// </summary>
    public async Task LeaveRoom()
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user?.CurrentRoomId is null)
        {
            return;
        }

        var roomId = user.CurrentRoomId;
        _stateService.LeaveRoom(userId, roomId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

        await Clients.Group(roomId).SendAsync("UserLeftRoom", new
        {
            UserId = userId,
            Username = user.Username,
            RoomId = roomId
        });

        await Clients.Caller.SendAsync("RoomLeft", roomId);

        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = UserStatus.Online,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE CALLS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initiate a private call with another user.
    /// </summary>
    public async Task CallUser(string targetUserId)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);
        var targetUser = _stateService.GetUserByUserId(targetUserId);

        if (user is null || targetUser is null)
        {
            await Clients.Caller.SendAsync("CallError", "User not found");
            return;
        }

        if (targetUser.PrivateCallWithUserId is not null)
        {
            await Clients.Caller.SendAsync("CallError", "User is already in a call");
            return;
        }

        var request = _stateService.InitiatePrivateCall(userId, targetUserId);
        if (request is null)
        {
            await Clients.Caller.SendAsync("CallError", "Failed to initiate call");
            return;
        }

        // Notify target user of incoming call
        await Clients.Client(targetUser.ConnectionId).SendAsync("IncomingCall", request);

        // Confirm to caller that request was sent
        await Clients.Caller.SendAsync("CallInitiated", new
        {
            TargetUserId = targetUserId,
            TargetUsername = targetUser.Username
        });
    }

    /// <summary>
    /// Accept an incoming private call.
    /// </summary>
    public async Task AcceptCall(string fromUserId)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);
        var fromUser = _stateService.GetUserByUserId(fromUserId);

        if (user is null || fromUser is null)
        {
            await Clients.Caller.SendAsync("CallError", "User not found");
            return;
        }

        if (!_stateService.AcceptPrivateCall(fromUserId, userId))
        {
            await Clients.Caller.SendAsync("CallError", "Failed to accept call");
            return;
        }

        // Create a private group for the call
        var callGroupId = $"call:{GetCallGroupId(userId, fromUserId)}";
        await Groups.AddToGroupAsync(Context.ConnectionId, callGroupId);
        await Groups.AddToGroupAsync(fromUser.ConnectionId, callGroupId);

        // Notify both parties
        var callInfo = new
        {
            CallGroupId = callGroupId,
            Participants = new[]
            {
                new { UserId = userId, Username = user.Username },
                new { UserId = fromUserId, Username = fromUser.Username }
            }
        };

        await Clients.Client(fromUser.ConnectionId).SendAsync("CallAccepted", callInfo);
        await Clients.Caller.SendAsync("CallConnected", callInfo);

        // Broadcast status changes
        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = UserStatus.InPrivateCall,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });

        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = fromUserId,
            Username = fromUser.Username,
            Status = UserStatus.InPrivateCall,
            IsMuted = fromUser.IsMuted,
            IsDeafened = fromUser.IsDeafened
        });
    }

    /// <summary>
    /// Decline an incoming private call.
    /// </summary>
    public async Task DeclineCall(string fromUserId)
    {
        var userId = GetUserId();
        var fromUser = _stateService.GetUserByUserId(fromUserId);

        _stateService.DeclinePrivateCall(fromUserId, userId);

        if (fromUser is not null)
        {
            await Clients.Client(fromUser.ConnectionId).SendAsync("CallDeclined", new
            {
                ByUserId = userId,
                Reason = "Declined"
            });
        }
    }

    /// <summary>
    /// End the current private call.
    /// </summary>
    public async Task EndCall()
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        var (otherUser, wasInCall) = _stateService.EndPrivateCall(userId);

        if (!wasInCall || user is null)
        {
            return;
        }

        var callGroupId = $"call:{GetCallGroupId(userId, otherUser?.UserId ?? "")}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, callGroupId);

        if (otherUser is not null)
        {
            await Groups.RemoveFromGroupAsync(otherUser.ConnectionId, callGroupId);
            await Clients.Client(otherUser.ConnectionId).SendAsync("CallEnded", new
            {
                EndedByUserId = userId,
                Reason = "Ended by user"
            });

            await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
            {
                UserId = otherUser.UserId,
                Username = otherUser.Username,
                Status = UserStatus.Online,
                IsMuted = otherUser.IsMuted,
                IsDeafened = otherUser.IsDeafened
            });
        }

        await Clients.Caller.SendAsync("CallEnded", new
        {
            EndedByUserId = userId,
            Reason = "You ended the call"
        });

        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = UserStatus.Online,
            IsMuted = user.IsMuted,
            IsDeafened = user.IsDeafened
        });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDIO STREAMING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Send audio data to the current room or private call.
    /// </summary>
    public async Task SendAudio(byte[] audioData, long sequenceNumber)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null || user.IsMuted)
        {
            return;
        }

        var packet = new AudioPacket
        {
            FromUserId = userId,
            AudioData = audioData,
            SequenceNumber = sequenceNumber,
            Format = new AudioFormat() // Use defaults
        };

        if (user.PrivateCallWithUserId is not null)
        {
            // Send to private call partner only
            var otherUser = _stateService.GetUserByUserId(user.PrivateCallWithUserId);
            if (otherUser is not null && !otherUser.IsDeafened)
            {
                await Clients.Client(otherUser.ConnectionId).SendAsync("ReceiveAudio", packet);
            }
        }
        else if (user.CurrentRoomId is not null)
        {
            // Send to all room members except self and deafened users
            var roomUsers = _stateService.GetUsersInRoom(user.CurrentRoomId)
                .Where(u => u.UserId != userId && !u.IsDeafened)
                .Select(u => u.ConnectionId);

            await Clients.Clients(roomUsers.ToList()).SendAsync("ReceiveAudio", packet);
        }
    }

    /// <summary>
    /// Notify others that user started speaking.
    /// </summary>
    public async Task StartSpeaking()
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null)
        {
            return;
        }

        var status = new SpeakingStatus { UserId = userId, IsSpeaking = true };

        if (user.PrivateCallWithUserId is not null)
        {
            var otherUser = _stateService.GetUserByUserId(user.PrivateCallWithUserId);
            if (otherUser is not null)
            {
                await Clients.Client(otherUser.ConnectionId).SendAsync("SpeakingStatusChanged", status);
            }
        }
        else if (user.CurrentRoomId is not null)
        {
            await Clients.OthersInGroup(user.CurrentRoomId).SendAsync("SpeakingStatusChanged", status);
        }
    }

    /// <summary>
    /// Notify others that user stopped speaking.
    /// </summary>
    public async Task StopSpeaking()
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null)
        {
            return;
        }

        var status = new SpeakingStatus { UserId = userId, IsSpeaking = false };

        if (user.PrivateCallWithUserId is not null)
        {
            var otherUser = _stateService.GetUserByUserId(user.PrivateCallWithUserId);
            if (otherUser is not null)
            {
                await Clients.Client(otherUser.ConnectionId).SendAsync("SpeakingStatusChanged", status);
            }
        }
        else if (user.CurrentRoomId is not null)
        {
            await Clients.OthersInGroup(user.CurrentRoomId).SendAsync("SpeakingStatusChanged", status);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // USER STATUS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Toggle mute status.
    /// </summary>
    public async Task SetMuted(bool isMuted)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null)
        {
            return;
        }

        _stateService.UpdateUserMuteStatus(userId, isMuted);

        // Notify relevant users
        if (user.PrivateCallWithUserId is not null)
        {
            var otherUser = _stateService.GetUserByUserId(user.PrivateCallWithUserId);
            if (otherUser is not null)
            {
                await Clients.Client(otherUser.ConnectionId).SendAsync("UserMuteChanged", new
                {
                    UserId = userId,
                    IsMuted = isMuted
                });
            }
        }
        else if (user.CurrentRoomId is not null)
        {
            await Clients.OthersInGroup(user.CurrentRoomId).SendAsync("UserMuteChanged", new
            {
                UserId = userId,
                IsMuted = isMuted
            });
        }

        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = GetUserStatus(user),
            RoomId = user.CurrentRoomId,
            IsMuted = isMuted,
            IsDeafened = user.IsDeafened
        });
    }

    /// <summary>
    /// Toggle deafen status.
    /// </summary>
    public async Task SetDeafened(bool isDeafened)
    {
        var userId = GetUserId();
        var user = _stateService.GetUserByUserId(userId);

        if (user is null)
        {
            return;
        }

        _stateService.UpdateUserDeafenStatus(userId, isDeafened);

        await Clients.Others.SendAsync("UserStatusChanged", new UserStatusUpdate
        {
            UserId = userId,
            Username = user.Username,
            Status = GetUserStatus(user),
            RoomId = user.CurrentRoomId,
            IsMuted = user.IsMuted,
            IsDeafened = isDeafened
        });
    }

    /// <summary>
    /// Get all online users.
    /// </summary>
    public Task<IReadOnlyList<UserStatusUpdate>> GetOnlineUsers()
    {
        var users = _stateService.GetAllUsers()
            .Select(u => new UserStatusUpdate
            {
                UserId = u.UserId,
                Username = u.Username,
                Status = GetUserStatus(u),
                RoomId = u.CurrentRoomId,
                IsMuted = u.IsMuted,
                IsDeafened = u.IsDeafened
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<UserStatusUpdate>>(users);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private string GetUserId()
    {
        return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? "";
    }

    private string GetUsername()
    {
        return Context.User?.FindFirst("preferred_username")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
            ?? Context.User?.FindFirst("name")?.Value
            ?? "Unknown";
    }

    private async Task SendCurrentState()
    {
        var rooms = _stateService.GetAllRooms();
        var users = _stateService.GetAllUsers()
            .Select(u => new UserStatusUpdate
            {
                UserId = u.UserId,
                Username = u.Username,
                Status = GetUserStatus(u),
                RoomId = u.CurrentRoomId,
                IsMuted = u.IsMuted,
                IsDeafened = u.IsDeafened
            })
            .ToList();

        await Clients.Caller.SendAsync("InitialState", new
        {
            Rooms = rooms,
            Users = users
        });
    }

    private static UserStatus GetUserStatus(ChatterUser user)
    {
        if (user.PrivateCallWithUserId is not null)
        {
            return UserStatus.InPrivateCall;
        }

        if (user.CurrentRoomId is not null)
        {
            return UserStatus.InRoom;
        }

        return UserStatus.Online;
    }

    private static string GetCallGroupId(string userId1, string userId2)
    {
        return string.CompareOrdinal(userId1, userId2) < 0
            ? $"{userId1}:{userId2}"
            : $"{userId2}:{userId1}";
    }
}
