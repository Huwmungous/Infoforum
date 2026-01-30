using LoggerWebService.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using IFGlobal.Logging;

namespace LoggerWebService.Controllers;

/// <summary>
/// Controller for log entry operations.
/// </summary>
[ApiController]
[Authorize]
public class LogController : ControllerBase
{
    private readonly LogEntryService _service;
    private readonly IHubContext<LogHub> _hubContext;
    private readonly ILogger<LogController> _logger;
    private readonly string _minimumLogLevel;

    private static readonly Dictionary<string, int> LogLevelPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Trace"] = 0,
        ["Debug"] = 1,
        ["Information"] = 2,
        ["Warning"] = 3,
        ["Error"] = 4,
        ["Critical"] = 5
    };

    public LogController(
        LogEntryService service,
        IHubContext<LogHub> hubContext,
        ILogger<LogController> logger,
        IConfiguration configuration)
    {
        _service = service;
        _hubContext = hubContext;
        _logger = logger;
        _minimumLogLevel = configuration.GetValue<string>("LoggerService:MinimumLogLevel") ?? "Information";
    }

    private bool IsLogLevelEnabled(string? logLevel)
    {
        if(string.IsNullOrEmpty(logLevel)) return true; // Default to enabled if no level specified

        var minPriority = LogLevelPriority.GetValueOrDefault(_minimumLogLevel, 2);
        var logPriority = LogLevelPriority.GetValueOrDefault(logLevel, 2);

        return logPriority >= minPriority;
    }

    /// <summary>
    /// Create a new log entry.
    /// </summary>
    /// <param name="request">The log entry to create.</param>
    /// <returns>The created log entry index.</returns>
    [HttpPost("api/logs")]
    [ProducesResponseType(typeof(LogCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddLogEntry([FromBody] LogEntryRequest request)
    {
        try
        {
            // Check if log level meets minimum threshold
            string? logLevel = null;
            if(request.LogData?.RootElement.TryGetProperty("level", out var levelElement) == true)
            {
                logLevel = levelElement.GetString();
            }

            if(!IsLogLevelEnabled(logLevel))
            {
                // Silently accept but don't store or broadcast
                return Ok(new LogCreatedResponse(-1, "Log entry below minimum level, not stored"));
            }

            var idx = await _service.AddLogEntryAsync(request);
            var createdAt = DateTime.UtcNow;

            // Broadcast to SignalR clients
            var logEntry = new LogEntryResponse(idx, request.Realm, request.Client, request.LogData, createdAt);
            await BroadcastLogEntryAsync(logEntry);

            return CreatedAtAction(
                nameof(GetLogEntry),
                new { idx },
                new LogCreatedResponse(idx, "Log entry created successfully"));
        }
        catch(ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid log entry request");
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch(PostgresException ex)
        {
            _logger.LogError(ex, "Database error creating log entry");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Database error");
        }
    }

    /// <summary>
    /// Get a single log entry by index.
    /// </summary>
    /// <param name="idx">The log entry index.</param>
    /// <returns>The log entry if found.</returns>
    [HttpGet("logs/{idx:int}")]
    [ProducesResponseType(typeof(LogEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogEntry(int idx)
    {
        var logEntry = await _service.GetLogEntryAsync(idx);

        if(logEntry is null)
        {
            return NotFound();
        }

        return Ok(logEntry);
    }

    /// <summary>
    /// Get log entries with pagination.
    /// </summary>
    /// <param name="limit">Maximum number of entries to return (default: 1000).</param>
    /// <param name="offset">Number of entries to skip (default: 0).</param>
    /// <returns>A list of log entries.</returns>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(List<LogEntryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogEntries(
        [FromQuery] int limit = 1000,
        [FromQuery] int offset = 0)
    {
        var logs = await _service.GetLogEntriesAsync(limit, offset);
        return Ok(logs);
    }

    /// <summary>
    /// Search log entries with filters.
    /// </summary>
    /// <param name="request">The search criteria.</param>
    /// <returns>A list of matching log entries.</returns>
    [HttpPost("logs/search")]
    [ProducesResponseType(typeof(List<LogEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SearchLogs([FromBody] LogSearchRequest request)
    {
        try
        {
            var logs = await _service.SearchLogEntriesAsync(request);
            return Ok(logs);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error searching logs");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Search error");
        }
    }

    /// <summary>
    /// Advanced search with grouped filter conditions.
    /// </summary>
    /// <param name="request">The advanced search criteria with filter groups.</param>
    /// <returns>A list of matching log entries.</returns>
    [HttpPost("logs/advanced-search")]
    [ProducesResponseType(typeof(List<LogEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AdvancedSearchLogs([FromBody] AdvancedLogSearchRequest request)
    {
        try
        {
            var logs = await _service.AdvancedSearchLogEntriesAsync(request);
            return Ok(logs);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error in advanced search");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Advanced search error");
        }
    }

    /// <summary>
    /// Notify SignalR clients of a new log entry (for backdoor logging path).
    /// Called by services that write directly to the log database.
    /// </summary>
    /// <param name="logEntry">The log entry to broadcast.</param>
    /// <returns>200 OK on success.</returns>
    [HttpPost("notify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> NotifyLogEntry([FromBody] LogEntryResponse logEntry)
    {
        await BroadcastLogEntryAsync(logEntry);
        return Ok();
    }

    private async Task BroadcastLogEntryAsync(LogEntryResponse logEntry)
    {
        await _hubContext.Clients.All.SendAsync("NewLogEntry", logEntry);
    }
}