namespace ChitterChatterWebService.Models;

/// <summary>
/// Represents a connected user in the voice chat system.
/// </summary>
public sealed class ChatterUser
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string ConnectionId { get; set; }
    public DateTimeOffset ConnectedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public string? CurrentRoomId { get; set; }
    public string? PrivateCallWithUserId { get; set; }
}

/// <summary>
/// Represents a voice chat room.
/// </summary>
public sealed class ChatterRoom
{
    public required string RoomId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? CreatedByUserId { get; init; }
    public int MaxParticipants { get; set; } = 50;
    public bool IsPrivate { get; set; }
    public HashSet<string> ParticipantUserIds { get; } = [];
}

/// <summary>
/// Audio data packet for voice transmission.
/// </summary>
public sealed class AudioPacket
{
    public required string FromUserId { get; init; }
    public required byte[] AudioData { get; init; }
    public required AudioFormat Format { get; init; }
    public long SequenceNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Audio format specification.
/// </summary>
public sealed class AudioFormat
{
    public int SampleRate { get; init; } = 48000;
    public int Channels { get; init; } = 1;
    public int BitsPerSample { get; init; } = 16;
    public string Codec { get; init; } = "opus";
}

/// <summary>
/// Request to initiate a private call.
/// </summary>
public sealed class PrivateCallRequest
{
    public required string FromUserId { get; init; }
    public required string FromUsername { get; init; }
    public required string ToUserId { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Response to a private call request.
/// </summary>
public sealed class PrivateCallResponse
{
    public required string FromUserId { get; init; }
    public required string ToUserId { get; init; }
    public required bool Accepted { get; init; }
    public string? DeclineReason { get; set; }
}

/// <summary>
/// Notification sent when users join/leave or change status.
/// </summary>
public sealed class UserStatusUpdate
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required UserStatus Status { get; init; }
    public string? RoomId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
}

/// <summary>
/// User connection status.
/// </summary>
public enum UserStatus
{
    Online,
    InRoom,
    InPrivateCall,
    Away,
    Offline
}

/// <summary>
/// Event raised when a user starts/stops speaking.
/// </summary>
public sealed class SpeakingStatus
{
    public required string UserId { get; init; }
    public required bool IsSpeaking { get; init; }
}

/// <summary>
/// Text message in a room (optional feature).
/// </summary>
public sealed class ChatMessage
{
    public required string MessageId { get; init; }
    public required string RoomId { get; init; }
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.UtcNow;
}
