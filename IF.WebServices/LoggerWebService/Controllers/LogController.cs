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
public class LogController(
    LogEntryService service,
    IHubContext<LogHub> hubContext,
    ILogger<LogController> logger) : ControllerBase
{
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
            if(request.LogData is null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid request",
                    Detail = "LogData is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            // Store everything - no filtering on receipt
            var idx = await service.AddLogEntryAsync(request);
            var createdAt = DateTime.UtcNow;

            // Get log level for filtered broadcast
            string? logLevel = null;
            if(request.LogData.RootElement.TryGetProperty("level", out var levelElement))
            {
                logLevel = levelElement.GetString();
            }

            // Broadcast to SignalR clients (filtered by their level preferences)
            var logEntry = new LogEntryResponse(idx, request.Realm, request.Client, request.LogData, createdAt);
            await BroadcastLogEntryAsync(logEntry, logLevel);

            return CreatedAtAction(
                nameof(GetLogEntry),
                new { idx },
                new LogCreatedResponse(idx, "Log entry created successfully"));
        }
        catch(ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid log entry request");
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch(PostgresException ex)
        {
            logger.LogError(ex, "Database error creating log entry");
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
        var logEntry = await service.GetLogEntryAsync(idx);

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
        var logs = await service.GetLogEntriesAsync(limit, offset);
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
            var logs = await service.SearchLogEntriesAsync(request);
            return Ok(logs);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error searching logs");
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
            var logs = await service.AdvancedSearchLogEntriesAsync(request);
            return Ok(logs);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error in advanced search");
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
        string? logLevel = null;
        if(logEntry.LogData?.RootElement.TryGetProperty("level", out var levelElement) == true)
        {
            logLevel = levelElement.GetString();
        }
        await BroadcastLogEntryAsync(logEntry, logLevel);
        return Ok();
    }

    private async Task BroadcastLogEntryAsync(LogEntryResponse logEntry, string? logLevel)
    {
        // Get connections that want this log level
        var eligibleConnections = LogHub.GetEligibleConnections(logLevel).ToList();

        if(eligibleConnections.Count > 0)
        {
            await hubContext.Clients.Clients(eligibleConnections).SendAsync("NewLogEntry", logEntry);
        }
    }
}