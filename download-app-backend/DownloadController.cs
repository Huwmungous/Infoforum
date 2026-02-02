using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChitterChatterDistribution.Controllers;

[ApiController]
public class DownloadController : ControllerBase
{
    private readonly DistributionOptions _options;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(IOptions<DistributionOptions> options, ILogger<DownloadController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Lists all available downloads.
    /// </summary>
    [HttpGet("/api/downloads")]
    [Authorize]
    public IActionResult ListDownloads()
    {
        var userInfo = GetUserInfo();

        _logger.LogInformation(
            "Downloads list requested - User: {User}, Email: {Email}, IP: {IP}",
            userInfo.Name, userInfo.Email, userInfo.IP);

        var downloads = new List<DownloadInfo>();
        var distPath = _options.DistributionPath;

        // Scan for available downloads
        if (Directory.Exists(distPath))
        {
            // ChitterChatter
            var chitterChatterZip = Path.Combine(distPath, "ChitterChatter-Setup.zip");
            if (System.IO.File.Exists(chitterChatterZip))
            {
                var fileInfo = new FileInfo(chitterChatterZip);
                var version = "1.0.0";

                var versionPath = Path.Combine(distPath, "version.txt");
                if (System.IO.File.Exists(versionPath))
                {
                    version = System.IO.File.ReadAllText(versionPath).Trim();
                }

                downloads.Add(new DownloadInfo
                {
                    Name = "ChitterChatter",
                    Description = "Voice chat client for teams - real-time communication with push-to-talk support.",
                    Filename = "ChitterChatter-Setup.zip",
                    Version = version,
                    Size = fileInfo.Length,
                    Platform = "Windows",
                    LastModified = fileInfo.LastWriteTimeUtc
                });
            }

            // Add more downloads here as they become available
            // Example:
            // var otherApp = Path.Combine(distPath, "OtherApp-Setup.zip");
            // if (System.IO.File.Exists(otherApp)) { ... }
        }

        return Ok(downloads);
    }

    /// <summary>
    /// Downloads a specific file.
    /// </summary>
    [HttpGet("/api/downloads/{filename}")]
    [Authorize]
    public IActionResult DownloadFile(string filename)
    {
        var userInfo = GetUserInfo();

        _logger.LogInformation(
            "Download requested - User: {User}, Email: {Email}, IP: {IP}, File: {Filename}",
            userInfo.Name, userInfo.Email, userInfo.IP, filename);

        // Security: only allow specific filenames, no path traversal
        if (string.IsNullOrEmpty(filename) || 
            filename.Contains("..") || 
            filename.Contains("/") || 
            filename.Contains("\\"))
        {
            _logger.LogWarning("Invalid filename requested: {Filename}", filename);
            return BadRequest("Invalid filename");
        }

        var filePath = Path.Combine(_options.DistributionPath, filename);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning(
                "Download failed - File not found: {Path}, User: {User}",
                filePath, userInfo.Name);
            return NotFound("File not available");
        }

        var fileInfo = new FileInfo(filePath);
        _logger.LogInformation(
            "Download started - User: {User}, Email: {Email}, IP: {IP}, File: {Filename}, Size: {Size} bytes",
            userInfo.Name, userInfo.Email, userInfo.IP, filename, fileInfo.Length);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = GetContentType(filename);
        return File(stream, contentType, filename);
    }

    /// <summary>
    /// Returns version information (public endpoint for update checks).
    /// </summary>
    [HttpGet("/api/version")]
    [AllowAnonymous]
    public IActionResult GetVersion()
    {
        var versionPath = Path.Combine(_options.DistributionPath, "version.txt");

        if (!System.IO.File.Exists(versionPath))
        {
            return Ok(new { version = "1.0.0" });
        }

        var version = System.IO.File.ReadAllText(versionPath).Trim();
        return Ok(new { version });
    }

    /// <summary>
    /// Health check endpoint.
    /// </summary>
    [HttpGet("/api/health")]
    [AllowAnonymous]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    private static string GetContentType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".zip" => "application/zip",
            ".exe" => "application/octet-stream",
            ".msi" => "application/x-msi",
            _ => "application/octet-stream"
        };
    }

    private (string Name, string Email, string IP) GetUserInfo()
    {
        var name = User.Identity?.Name ?? "anonymous";
        var email = User.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',').First().Trim();
        }
        else if (Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            ip = realIp.ToString();
        }

        return (name, email, ip);
    }
}

public class DownloadInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Filename { get; set; } = "";
    public string Version { get; set; } = "";
    public long Size { get; set; }
    public string Platform { get; set; } = "";
    public DateTime LastModified { get; set; }
}
