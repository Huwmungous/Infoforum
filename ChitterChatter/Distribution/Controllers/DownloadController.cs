using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChitterChatterDistribution.Controllers;

[ApiController]
[Authorize]
public class DownloadController : ControllerBase
{
    private readonly DistributionOptions _options;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(IOptions<DistributionOptions> options, ILogger<DownloadController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("/download")]
    public IActionResult Download()
    {
        var zipPath = Path.Combine(_options.DistributionPath, "ChitterChatter-Setup.zip");
        
        if (!System.IO.File.Exists(zipPath))
        {
            _logger.LogWarning("Download requested but file not found: {Path}", zipPath);
            return NotFound("Distribution file not available");
        }

        _logger.LogInformation("User {User} downloading ChitterChatter", User.Identity?.Name);
        
        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/zip", "ChitterChatter-Setup.zip");
    }
}
