using Microsoft.AspNetCore.Mvc;

namespace IFOllama.WebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagController : ControllerBase
{
    /// <summary>
    /// Basic health check endpoint.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "IFOllama.WebService",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Returns service version information.
    /// </summary>
    [HttpGet("version")]
    public IActionResult Version()
    {
        var assembly = typeof(DiagController).Assembly;
        var version = assembly.GetName().Version?.ToString() ?? "unknown";

        return Ok(new
        {
            version,
            framework = Environment.Version.ToString(),
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
