using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using IFGlobal.Configuration;
using IFGlobal.Models;

namespace IFGlobal.Controllers;

/// <summary>
/// Provides a standard health check endpoint for all SfD web services.
/// </summary>
[ApiController]
[Route("[controller]")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly HealthControllerOptions _options;

    /// <summary>
    /// Creates a new instance of the HealthController.
    /// </summary>
    /// <param name="options">Optional configuration options.</param>
    public HealthController(IOptions<HealthControllerOptions>? options = null)
    {
        _options = options?.Value ?? new HealthControllerOptions();
    }

    /// <summary>
    /// Returns the health status of the service.
    /// </summary>
    /// <returns>Health status with timestamp and optional service information.</returns>
    /// <response code="200">Service is healthy.</response>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var response = new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Service = _options.ServiceName,
            Version = _options.ServiceVersion
        };

        return Ok(response);
    }
}
