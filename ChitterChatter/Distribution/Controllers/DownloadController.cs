using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ChitterChatterDistribution.Controllers;

[ApiController]
[Authorize(Policy = "NginxOrJwt")]
public class DownloadController : ControllerBase
{
    private readonly DistributionOptions _options;
    private readonly ILogger<DownloadController> _logger;

    public DownloadController(IOptions<DistributionOptions> options, ILogger<DownloadController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        var userInfo = GetUserInfo();

        _logger.LogInformation(
            "Download page visited - User: {User}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}, AuthMethod: {AuthMethod}",
            userInfo.Name, userInfo.Email, userInfo.IP, userInfo.UserAgent, userInfo.AuthMethod);

        var distPath = _options.DistributionPath;
        var zipPath = Path.Combine(distPath, "ChitterChatter-Setup.zip");

        var version = "1.0.0";
        var fileSize = "";
        var isAvailable = false;

        if (System.IO.File.Exists(zipPath))
        {
            isAvailable = true;
            var fileInfo = new FileInfo(zipPath);
            fileSize = FormatFileSize(fileInfo.Length);

            var versionPath = Path.Combine(distPath, "version.txt");
            if (System.IO.File.Exists(versionPath))
            {
                version = System.IO.File.ReadAllText(versionPath).Trim();
            }
        }
        else
        {
            _logger.LogWarning("Distribution file not found: {Path}", zipPath);
        }

        var html = GenerateHtml(version, fileSize, isAvailable, userInfo.Name);
        return Content(html, "text/html");
    }

    [HttpGet("/download")]
    public IActionResult Download()
    {
        var zipPath = Path.Combine(_options.DistributionPath, "ChitterChatter-Setup.zip");
        var userInfo = GetUserInfo();

        _logger.LogInformation(
            "Download requested - User: {User}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}, AuthMethod: {AuthMethod}",
            userInfo.Name, userInfo.Email, userInfo.IP, userInfo.UserAgent, userInfo.AuthMethod);

        if (!System.IO.File.Exists(zipPath))
        {
            _logger.LogWarning(
                "Download failed - File not found: {Path}, User: {User}, IP: {IP}",
                zipPath, userInfo.Name, userInfo.IP);
            return NotFound("Distribution file not available");
        }

        var fileInfo = new FileInfo(zipPath);
        _logger.LogInformation(
            "Download started - User: {User}, Email: {Email}, IP: {IP}, File: {FileName}, Size: {Size} bytes",
            userInfo.Name, userInfo.Email, userInfo.IP, "ChitterChatter-Setup.zip", fileInfo.Length);

        var stream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/zip", "ChitterChatter-Setup.zip");
    }

    [HttpGet("/version")]
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

    private (string Name, string Email, string IP, string UserAgent, string AuthMethod) GetUserInfo()
    {
        var name = User.Identity?.Name ?? "anonymous";
        var email = User.Claims.FirstOrDefault(c => c.Type == "email")?.Value ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var authMethod = User.Identity?.IsAuthenticated == true ? "JWT" : "Nginx-Proxy";
        if (Request.Headers.TryGetValue("X-Nginx-Proxy", out var nginxHeader) && nginxHeader == "authenticated")
        {
            authMethod = User.Identity?.IsAuthenticated == true ? "JWT+Nginx" : "Nginx-Proxy";
        }

        if (Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',').First().Trim();
        }
        else if (Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            ip = realIp.ToString();
        }

        return (name, email, ip, userAgent, authMethod);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private static string GenerateHtml(string version, string fileSize, bool isAvailable, string userName)
    {
        var downloadSection = isAvailable
            ? $"""
                <div class="download-card">
                    <h2>ChitterChatter Desktop Client</h2>
                    <p class="version">Version {version}</p>
                    <p class="size">Size: {fileSize}</p>
                    <a href="download" class="download-btn">Download Installer</a>
                </div>
              """
            : """
                <div class="download-card unavailable">
                    <h2>ChitterChatter Desktop Client</h2>
                    <p>The installer is not currently available.</p>
                    <p>Please contact your administrator.</p>
                </div>
              """;

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>ChitterChatter Download</title>
                <style>
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
                        background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                        min-height: 100vh;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        color: #fff;
                    }
                    .container {
                        text-align: center;
                        padding: 2rem;
                    }
                    h1 {
                        font-size: 2.5rem;
                        margin-bottom: 0.5rem;
                        background: linear-gradient(90deg, #00d4ff, #7b2cbf);
                        -webkit-background-clip: text;
                        -webkit-text-fill-color: transparent;
                        background-clip: text;
                    }
                    .subtitle {
                        color: #8892b0;
                        margin-bottom: 2rem;
                    }
                    .download-card {
                        background: rgba(255, 255, 255, 0.05);
                        border: 1px solid rgba(255, 255, 255, 0.1);
                        border-radius: 16px;
                        padding: 2rem 3rem;
                        backdrop-filter: blur(10px);
                    }
                    .download-card h2 {
                        font-size: 1.5rem;
                        margin-bottom: 1rem;
                    }
                    .version, .size {
                        color: #8892b0;
                        margin-bottom: 0.5rem;
                    }
                    .download-btn {
                        display: inline-block;
                        margin-top: 1.5rem;
                        padding: 1rem 2rem;
                        background: linear-gradient(90deg, #00d4ff, #7b2cbf);
                        color: #fff;
                        text-decoration: none;
                        border-radius: 8px;
                        font-weight: 600;
                        transition: transform 0.2s, box-shadow 0.2s;
                    }
                    .download-btn:hover {
                        transform: translateY(-2px);
                        box-shadow: 0 10px 30px rgba(0, 212, 255, 0.3);
                    }
                    .unavailable {
                        border-color: rgba(255, 100, 100, 0.3);
                    }
                    .user-info {
                        margin-top: 2rem;
                        color: #8892b0;
                        font-size: 0.9rem;
                    }
                </style>
            </head>
            <body>
                <div class="container">
                    <h1>ChitterChatter</h1>
                    <p class="subtitle">Voice Chat for Teams</p>
                    {{downloadSection}}
                    <p class="user-info">Logged in as: {{userName}}</p>
                </div>
            </body>
            </html>
            """;
    }
}
