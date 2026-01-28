using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IFGlobal.Logging;

namespace LoggerWebService.Controllers;

/// <summary>
/// Controller for retrieving filter values used by the log viewer (dropdowns).
/// </summary>
[ApiController]
[Authorize]
public class FiltersController(
    LogEntryService service,
    ILogger<FiltersController> logger) : ControllerBase
{
    /// <summary>
    /// Get distinct environments.
    /// </summary>
    [HttpGet("filters/environments")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEnvironments(CancellationToken ct)
    {
        try
        {
            var results = await service.GetEnvironmentFiltersAsync(ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving environment filters");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Filter retrieval error");
        }
    }

    /// <summary>
    /// Get distinct realms (optionally filtered by environment).
    /// </summary>
    [HttpGet("filters/realms")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRealms([FromQuery] string? environment, CancellationToken ct)
    {
        try
        {
            var results = await service.GetRealmFiltersAsync(environment, ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving realm filters (environment={Environment})", environment);
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Filter retrieval error");
        }
    }

    /// <summary>
    /// Get distinct clients (optionally filtered by realm).
    /// </summary>
    [HttpGet("filters/clients")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetClients([FromQuery] string? realm, CancellationToken ct)
    {
        try
        {
            var results = await service.GetClientFiltersAsync(realm: realm, ct :ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving client filters (realm={Realm})", realm);
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Filter retrieval error");
        }
    }

    /// <summary>
    /// Get distinct applications.
    /// </summary>
    [HttpGet("filters/applications")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetApplications(CancellationToken ct)
    {
        try
        {
            var results = await service.GetApplicationFiltersAsync(ct: ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving application filters");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Filter retrieval error");
        }
    }

    /// <summary>
    /// Get distinct log levels.
    /// </summary>
    [HttpGet("filters/log-levels")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLogLevels(CancellationToken ct)
    {
        try
        {
            var results = await service.GetLogLevelFiltersAsync(ct);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving log level filters");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Filter retrieval error");
        }
    }
}
