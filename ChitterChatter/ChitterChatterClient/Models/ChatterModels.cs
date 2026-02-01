namespace ChitterChatterClient.Models;

/// <summary>
/// Represents a user in the chat system.
/// </summary>
public sealed class ChatterUser
{
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public UserStatus Status { get; set; } = UserStatus.Online;
    public string? RoomId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
}

/// <summary>
/// Represents a voice chat room.
/// </summary>
public sealed class ChatterRoom
{
    public required string RoomId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; set; }
    public int ParticipantCount { get; set; }
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
    public DateTimeOffset Timestamp { get; init; }
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
    public DateTimeOffset RequestedAt { get; init; }
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
/// User status update notification.
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
/// Speaking status change notification.
/// </summary>
public sealed class SpeakingStatus
{
    public required string UserId { get; init; }
    public required bool IsSpeaking { get; init; }
}

/// <summary>
/// Connection state for the SignalR hub.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Failed
}
