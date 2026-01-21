using System.Collections.Concurrent;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for managing analysis sessions, allowing long-running operations to be tracked and resumed.
/// </summary>
public class SessionService
{
    private readonly ConcurrentDictionary<string, AnalysisSession> _sessions = new();
    private readonly ILogger<SessionService> _logger;

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a new analysis session.
    /// </summary>
    public AnalysisSession CreateSession()
    {
        var session = new AnalysisSession();
        _sessions[session.SessionId] = session;
        _logger.LogInformation("Created session {SessionId}", session.SessionId);
        return session;
    }

    /// <summary>
    /// Gets an existing session by ID.
    /// </summary>
    public AnalysisSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// Updates session status and logs progress.
    /// </summary>
    public void UpdateSession(string sessionId, Action<AnalysisSession> update)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            update(session);
        }
    }

    /// <summary>
    /// Adds a log entry to a session.
    /// </summary>
    public void Log(string sessionId, string message)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
            session.Log.Add($"[{timestamp}] {message}");
            _logger.LogDebug("[{SessionId}] {Message}", sessionId, message);
        }
    }

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    public IEnumerable<AnalysisSession> GetActiveSessions()
    {
        return _sessions.Values.Where(s => s.Status != SessionStatus.Completed && s.Status != SessionStatus.Failed);
    }

    /// <summary>
    /// Removes a session (for cleanup).
    /// </summary>
    public bool RemoveSession(string sessionId)
    {
        var removed = _sessions.TryRemove(sessionId, out _);
        if (removed)
        {
            _logger.LogInformation("Removed session {SessionId}", sessionId);
        }
        return removed;
    }

    /// <summary>
    /// Cleans up old completed sessions.
    /// </summary>
    public int CleanupOldSessions(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var toRemove = _sessions
            .Where(kvp => kvp.Value.CreatedAt < cutoff && 
                         (kvp.Value.Status == SessionStatus.Completed || kvp.Value.Status == SessionStatus.Failed))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in toRemove)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        if (toRemove.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} old sessions", toRemove.Count);
        }

        return toRemove.Count;
    }
}
