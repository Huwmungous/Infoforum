using System.Text.Json;

namespace IFGlobal.Logging;

/// <summary>
/// Request to create a new log entry.
/// </summary>
/// <param name="Realm">The realm/tenant identifier.</param>
/// <param name="Client">The client application identifier.</param>
/// <param name="LogData">The log data as JSON.</param>
/// <param name="Environment">Optional. Environment name (e.g. DEV/SIT/PROD). If omitted, may be extracted from LogData.</param>
/// <param name="Application">Optional. Application/service name. If omitted, may be extracted from LogData.</param>
/// <param name="LogLevel">Optional. Log severity/level (e.g. Debug/Information/Warning/Error). If omitted, may be extracted from LogData.</param>
public class LogEntryRequest
{
    public required string Realm { get; set; }
    public required string Client { get; set; }
    public required JsonDocument LogData { get; set; }
    public string? Environment { get; set; }
    public string? Application { get; set; }
    public string? LogLevel { get; set; }
}

/// <summary>
/// Response containing a log entry.
/// </summary>
/// <param name="Idx">The log entry index.</param>
/// <param name="Realm">The realm/tenant identifier.</param>
/// <param name="Client">The client application identifier.</param>
/// <param name="LogData">The log data as JSON.</param>
/// <param name="CreatedAt">The timestamp when the log entry was created.</param>
public record LogEntryResponse(int Idx, string Realm, string Client, JsonDocument LogData, DateTime CreatedAt);

/// <summary>
/// Response when a log entry is created.
/// </summary>
/// <param name="Idx">The created log entry index.</param>
/// <param name="Message">A success message.</param>
public record LogCreatedResponse(int Idx, string Message);