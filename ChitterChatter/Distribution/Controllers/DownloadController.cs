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

    [HttpGet("/download")]
    public IActionResult Download()
    {
        var zipPath = Path.Combine(_options.DistributionPath, "ChitterChatter-Setup.zip");
        var userInfo = GetUserInfo();

        _logger.LogInformation(
            "Download requested - User: {User}, Email: {Email}, IP: {IP}, UserAgent: {UserAgent}, AuthMethod: {AuthMethod}",
            userInfo.Name, userInfo.Email, userInfo.IP, userInfo.UserAgent, userInfo.AuthMethod);

        if(!System.IO.File.Exists(zipPath))
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

        if(!System.IO.File.Exists(versionPath))
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

        // Determine auth method
        var authMethod = User.Identity?.IsAuthenticated == true ? "JWT" : "Nginx-Proxy";
        if(Request.Headers.TryGetValue("X-Nginx-Proxy", out var nginxHeader) && nginxHeader == "authenticated")
        {
            authMethod = User.Identity?.IsAuthenticated == true ? "JWT+Nginx" : "Nginx-Proxy";
        }

        // Get real IP if behind proxy
        if(Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',').First().Trim();
        }
        else if(Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            ip = realIp.ToString();
        }

        return (name, email, ip, userAgent, authMethod);
    }
}