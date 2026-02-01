using System.Collections.Concurrent;
using ChitterChatterWebService.Models;

namespace ChitterChatterWebService.Services;

/// <summary>
/// Manages the state of all connected users, rooms, and private calls.
/// Thread-safe for concurrent SignalR hub access.
/// </summary>
public sealed class ChatterStateService
{
    private readonly ConcurrentDictionary<string, ChatterUser> _usersByUserId = new();
    private readonly ConcurrentDictionary<string, ChatterUser> _usersByConnectionId = new();
    private readonly ConcurrentDictionary<string, ChatterRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, PrivateCallRequest> _pendingCalls = new();
    private readonly ILogger<ChatterStateService> _logger;

    public ChatterStateService(ILogger<ChatterStateService> logger)
    {
        _logger = logger;
        InitialiseDefaultRooms();
    }

    private void InitialiseDefaultRooms()
    {
        var lobby = new ChatterRoom
        {
            RoomId = "lobby",
            Name = "Lobby",
            Description = "General discussion room",
            CreatedByUserId = "system"
        };
        _rooms.TryAdd(lobby.RoomId, lobby);

        var support = new ChatterRoom
        {
            RoomId = "support",
            Name = "Support",
            Description = "Technical support channel",
            CreatedByUserId = "system"
        };
        _rooms.TryAdd(support.RoomId, support);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // USER MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    public ChatterUser? AddUser(string userId, string username, string connectionId)
    {
        var user = new ChatterUser
        {
            UserId = userId,
            Username = username,
            ConnectionId = connectionId
        };

        // If user already exists, update connection ID (reconnection scenario)
        if (_usersByUserId.TryGetValue(userId, out var existingUser))
        {
            _usersByConnectionId.TryRemove(existingUser.ConnectionId, out _);
            existingUser.ConnectionId = connectionId;
            _usersByConnectionId.TryAdd(connectionId, existingUser);
            _logger.LogInformation("User {Username} reconnected with new connection", username);
            return existingUser;
        }

        if (_usersByUserId.TryAdd(userId, user) && _usersByConnectionId.TryAdd(connectionId, user))
        {
            _logger.LogInformation("User {Username} ({UserId}) connected", username, userId);
            return user;
        }

        return null;
    }

    public ChatterUser? RemoveUserByConnectionId(string connectionId)
    {
        if (_usersByConnectionId.TryRemove(connectionId, out var user))
        {
            _usersByUserId.TryRemove(user.UserId, out _);

            // Clean up room membership
            if (user.CurrentRoomId is not null)
            {
                LeaveRoom(user.UserId, user.CurrentRoomId);
            }

            // Clean up any pending calls
            CancelPendingCallsForUser(user.UserId);

            _logger.LogInformation("User {Username} ({UserId}) disconnected", user.Username, user.UserId);
            return user;
        }

        return null;
    }

    public ChatterUser? GetUserByUserId(string userId)
    {
        return _usersByUserId.GetValueOrDefault(userId);
    }

    public ChatterUser? GetUserByConnectionId(string connectionId)
    {
        return _usersByConnectionId.GetValueOrDefault(connectionId);
    }

    public IReadOnlyList<ChatterUser> GetAllUsers()
    {
        return _usersByUserId.Values.ToList();
    }

    public IReadOnlyList<ChatterUser> GetUsersInRoom(string roomId)
    {
        return _usersByUserId.Values
            .Where(u => u.CurrentRoomId == roomId)
            .ToList();
    }

    public void UpdateUserMuteStatus(string userId, bool isMuted)
    {
        if (_usersByUserId.TryGetValue(userId, out var user))
        {
            user.IsMuted = isMuted;
        }
    }

    public void UpdateUserDeafenStatus(string userId, bool isDeafened)
    {
        if (_usersByUserId.TryGetValue(userId, out var user))
        {
            user.IsDeafened = isDeafened;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ROOM MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    public IReadOnlyList<ChatterRoom> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    public ChatterRoom? GetRoom(string roomId)
    {
        return _rooms.GetValueOrDefault(roomId);
    }

    public ChatterRoom? CreateRoom(string roomId, string name, string? description, string createdByUserId)
    {
        var room = new ChatterRoom
        {
            RoomId = roomId,
            Name = name,
            Description = description,
            CreatedByUserId = createdByUserId
        };

        if (_rooms.TryAdd(roomId, room))
        {
            _logger.LogInformation("Room {Name} ({RoomId}) created by {UserId}", name, roomId, createdByUserId);
            return room;
        }

        return null;
    }

    public bool JoinRoom(string userId, string roomId)
    {
        if (!_usersByUserId.TryGetValue(userId, out var user))
        {
            return false;
        }

        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return false;
        }

        // Leave current room if in one
        if (user.CurrentRoomId is not null && user.CurrentRoomId != roomId)
        {
            LeaveRoom(userId, user.CurrentRoomId);
        }

        // End any private call
        if (user.PrivateCallWithUserId is not null)
        {
            EndPrivateCall(userId);
        }

        user.CurrentRoomId = roomId;
        room.ParticipantUserIds.Add(userId);

        _logger.LogInformation("User {Username} joined room {RoomName}", user.Username, room.Name);
        return true;
    }

    public bool LeaveRoom(string userId, string roomId)
    {
        if (!_usersByUserId.TryGetValue(userId, out var user))
        {
            return false;
        }

        if (!_rooms.TryGetValue(roomId, out var room))
        {
            return false;
        }

        user.CurrentRoomId = null;
        room.ParticipantUserIds.Remove(userId);

        _logger.LogInformation("User {Username} left room {RoomName}", user.Username, room.Name);
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE CALLS
    // ═══════════════════════════════════════════════════════════════════════════

    public PrivateCallRequest? InitiatePrivateCall(string fromUserId, string toUserId)
    {
        if (!_usersByUserId.TryGetValue(fromUserId, out var fromUser))
        {
            return null;
        }

        if (!_usersByUserId.TryGetValue(toUserId, out var toUser))
        {
            return null;
        }

        // Check if either user is already in a private call
        if (fromUser.PrivateCallWithUserId is not null || toUser.PrivateCallWithUserId is not null)
        {
            return null;
        }

        var request = new PrivateCallRequest
        {
            FromUserId = fromUserId,
            FromUsername = fromUser.Username,
            ToUserId = toUserId
        };

        var callKey = GetCallKey(fromUserId, toUserId);
        if (_pendingCalls.TryAdd(callKey, request))
        {
            _logger.LogInformation("Private call initiated: {FromUser} -> {ToUser}", 
                fromUser.Username, toUser.Username);
            return request;
        }

        return null;
    }

    public bool AcceptPrivateCall(string fromUserId, string toUserId)
    {
        var callKey = GetCallKey(fromUserId, toUserId);
        if (!_pendingCalls.TryRemove(callKey, out _))
        {
            return false;
        }

        if (!_usersByUserId.TryGetValue(fromUserId, out var fromUser) ||
            !_usersByUserId.TryGetValue(toUserId, out var toUser))
        {
            return false;
        }

        // Leave any current rooms
        if (fromUser.CurrentRoomId is not null)
        {
            LeaveRoom(fromUserId, fromUser.CurrentRoomId);
        }
        if (toUser.CurrentRoomId is not null)
        {
            LeaveRoom(toUserId, toUser.CurrentRoomId);
        }

        fromUser.PrivateCallWithUserId = toUserId;
        toUser.PrivateCallWithUserId = fromUserId;

        _logger.LogInformation("Private call established: {FromUser} <-> {ToUser}",
            fromUser.Username, toUser.Username);
        return true;
    }

    public bool DeclinePrivateCall(string fromUserId, string toUserId)
    {
        var callKey = GetCallKey(fromUserId, toUserId);
        if (_pendingCalls.TryRemove(callKey, out _))
        {
            _logger.LogInformation("Private call declined: {FromUserId} -> {ToUserId}", fromUserId, toUserId);
            return true;
        }
        return false;
    }

    public (ChatterUser? otherUser, bool wasInCall) EndPrivateCall(string userId)
    {
        if (!_usersByUserId.TryGetValue(userId, out var user))
        {
            return (null, false);
        }

        var otherUserId = user.PrivateCallWithUserId;
        if (otherUserId is null)
        {
            return (null, false);
        }

        user.PrivateCallWithUserId = null;

        if (_usersByUserId.TryGetValue(otherUserId, out var otherUser))
        {
            otherUser.PrivateCallWithUserId = null;
            _logger.LogInformation("Private call ended: {User1} <-> {User2}",
                user.Username, otherUser.Username);
            return (otherUser, true);
        }

        return (null, true);
    }

    public PrivateCallRequest? GetPendingCall(string fromUserId, string toUserId)
    {
        var callKey = GetCallKey(fromUserId, toUserId);
        return _pendingCalls.GetValueOrDefault(callKey);
    }

    private void CancelPendingCallsForUser(string userId)
    {
        var keysToRemove = _pendingCalls
            .Where(kvp => kvp.Value.FromUserId == userId || kvp.Value.ToUserId == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _pendingCalls.TryRemove(key, out _);
        }
    }

    private static string GetCallKey(string userId1, string userId2)
    {
        // Consistent key regardless of direction
        return string.CompareOrdinal(userId1, userId2) < 0
            ? $"{userId1}:{userId2}"
            : $"{userId2}:{userId1}";
    }
}
