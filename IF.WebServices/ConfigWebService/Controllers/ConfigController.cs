using ConfigWebService.Models;
using ConfigWebService.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ConfigWebService.Controllers;

/// <summary>
/// Configuration endpoint that provides client configuration based on cfg and type parameters
/// </summary>
[ApiController]
[Route("[controller]")]
public class ConfigController(
    ILogger<ConfigController> logger,
    ConfigService configService) : ControllerBase
{
    /// <summary>
    /// Get configuration based on cfg type and user/service type
    /// </summary>
    /// <param name="cfg">Configuration type (e.g., 'bootstrap', 'logLevel')</param>
    /// <param name="type">Client type: 'user' or 'service'</param>
    /// <param name="appDomain">Application domain (e.g., 'Infoforum', 'BreakTackle')</param>
    /// <param name="app">Requesting application name (for diagnostic logging)</param>
    /// <returns>Configuration object appropriate for the request</returns>
    /// <response code="200">Returns the requested configuration</response>
    /// <response code="400">If cfg or type parameter is missing or invalid</response>
    /// <response code="401">If authentication token is missing or invalid</response>
    /// <response code="404">If configuration entry not found</response>
    /// <response code="500">If an internal error occurs</response>
    [HttpGet]
    [ProducesResponseType(typeof(JsonElement), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get(
        [FromQuery] string cfg,
        [FromQuery] string type,
        [FromQuery] string appDomain,
        [FromQuery] string? app = null)
    {
        // Default app name if not provided
        var appName = string.IsNullOrWhiteSpace(app) ? "Unknown" : app;

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(cfg))
            return BadRequest(new ErrorResponse { Error = "Parameter 'cfg' is required" });

        if (string.IsNullOrWhiteSpace(type))
            return BadRequest(new ErrorResponse { Error = "Parameter 'type' is required" });

        if (string.IsNullOrWhiteSpace(appDomain))
            return BadRequest(new ErrorResponse { Error = "Parameter 'appDomain' is required" });

        // Normalise to lowercase for case-insensitive comparison
        cfg = cfg.ToLowerInvariant();
        type = type.ToLowerInvariant();

        if (type != "user" && type != "service")
            return BadRequest(new ErrorResponse { Error = "Parameter 'type' must be 'user' or 'service'" });

        // If cfg is 'bootstrap', fetch bootstrap record and return appropriate clientId
        if (cfg == "bootstrap")
        {
            return await GetBootstrapConfig(appDomain, type, appName);
        }

        // For all other cfg values, require authentication
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        // Query the database for the specific config value from user/service record
        return await GetConfigValue(appDomain, type, cfg, appName);
    }

    /// <summary>
    /// Get Bootstrap configuration from database
    /// Fetches the bootstrap record and returns clientId based on type (userClientId or serviceClientId)
    /// </summary>
    private async Task<IActionResult> GetBootstrapConfig(string appDomain, string type, string appName)
    {
        try
        {
            // Fetch the bootstrap record
            var entry = await configService.GetByAppDomainAndTypeAsync(appDomain, "bootstrap", enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "{AppDomain}.{AppName} requested bootstrap config - not found or disabled",
                    appDomain, appName);
                return NotFound(new ErrorResponse
                {
                    Error = $"Bootstrap configuration not found for appDomain '{appDomain}'"
                });
            }

            if (entry.Config is null)
            {
                logger.LogWarning(
                    "{AppDomain}.{AppName} requested bootstrap config - config is null",
                    appDomain, appName);
                return NotFound(new ErrorResponse
                {
                    Error = $"Bootstrap configuration not set for appDomain '{appDomain}'"
                });
            }

            logger.LogInformation(
                "{AppDomain}.{AppName} ({Type}) requested bootstrap config",
                appDomain, appName, type);

            var configJson = entry.Config.RootElement;
            
            // Select clientId based on type
            string? clientId;
            if (type == "service")
            {
                clientId = configJson.TryGetProperty("serviceClientId", out var svcClientIdElement)
                    ? svcClientIdElement.GetString() : null;
            }
            else
            {
                clientId = configJson.TryGetProperty("userClientId", out var userClientIdElement)
                    ? userClientIdElement.GetString() : null;
            }
            
            // Build the response object
            var response = new Dictionary<string, object?>
            {
                ["clientId"] = clientId,
                ["realm"] = configJson.TryGetProperty("realm", out var realm) 
                    ? realm.GetString() : null,
                ["openIdConfig"] = configJson.TryGetProperty("openIdConfig", out var openIdConfig) 
                    ? openIdConfig.GetString() : null,
                ["loggerService"] = configJson.TryGetProperty("loggerService", out var loggerService) 
                    ? loggerService.GetString() : null
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{AppDomain}.{AppName} ({Type}) requested bootstrap config - error occurred",
                appDomain, appName, type);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific configuration value from user or service record
    /// </summary>
    private async Task<IActionResult> GetConfigValue(string appDomain, string type, string configKey, string appName)
    {
        try
        {
            // Fetch the user or service record based on type
            var entry = await configService.GetByAppDomainAndTypeAsync(appDomain, type, enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "{AppDomain}.{AppName} ({Type}) requested config - entry not found or disabled",
                    appDomain, appName, type);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration not found for appDomain '{appDomain}', type '{type}'"
                });
            }

            var value = entry.GetConfigValue(configKey);

            if (value is null)
            {
                logger.LogWarning(
                    "{AppDomain}.{AppName} ({Type}) requested config key '{ConfigKey}' - not found",
                    appDomain, appName, type, configKey);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration key '{configKey}' not found"
                });
            }

            logger.LogInformation(
                "{AppDomain}.{AppName} ({Type}) requested config key '{ConfigKey}'",
                appDomain, appName, type, configKey);

            return Ok(value.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{AppDomain}.{AppName} ({Type}) requested config key '{ConfigKey}' - error occurred",
                appDomain, appName, type, configKey);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}