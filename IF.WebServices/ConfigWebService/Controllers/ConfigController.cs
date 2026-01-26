using ConfigWebService.Models;
using ConfigWebService.Services;
using Microsoft.AspNetCore.Mvc;
using IFGlobal.Models;
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
    /// <param name="cfg">Configuration type (e.g., 'oidc', 'bootstrap', 'loggerdb')</param>
    /// <param name="type">Client type: 'user' or 'service'</param>
    /// <param name="appDomain">Application domain (e.g., 'Infoforum', 'BreakTackle')</param>
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
        [FromQuery] string appDomain)
    {
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

        // If cfg is 'bootstrap', allow unauthenticated access
        if (cfg == "bootstrap")
        {
            return await GetBootstrapConfigFromDb(appDomain);
        }

        // For all other cfg values, require authentication
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        // Query the database for the configuration
        return await GetConfigValueFromDb(appDomain, type, cfg);
    }

    /// <summary>
    /// Get Bootstrap configuration from database
    /// Returns configuration including realm, clientId, openIdConfig etc.
    /// </summary>
    private async Task<IActionResult> GetBootstrapConfigFromDb(string appDomain)
    {
        try
        {
            // Only return enabled entries for config requests
            var entry = await configService.GetByAppDomainAsync(appDomain, enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "Configuration entry not found or disabled for appDomain={AppDomain}",
                    appDomain);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration not found for appDomain '{appDomain}'"
                });
            }

            if (entry.BootstrapConfig is null)
            {
                logger.LogWarning(
                    "Bootstrap config is null for appDomain={AppDomain}",
                    appDomain);
                return NotFound(new ErrorResponse
                {
                    Error = $"Bootstrap configuration not set for appDomain '{appDomain}'"
                });
            }

            logger.LogInformation(
                "Returning bootstrap config for appDomain={AppDomain}",
                appDomain);

            // Transform the bootstrap config to return the appropriate values
            var bootstrapJson = entry.BootstrapConfig.RootElement;
            
            // Get values from JSONB
            string? realm = bootstrapJson.TryGetProperty("realm", out var realmElement)
                ? realmElement.GetString() : null;
            string? clientId = bootstrapJson.TryGetProperty("clientId", out var clientIdElement)
                ? clientIdElement.GetString() : null;
            
            // Build the response object
            var response = new Dictionary<string, object?>
            {
                ["realm"] = realm,
                ["clientId"] = clientId,
                ["openIdConfig"] = bootstrapJson.TryGetProperty("openIdConfig", out var openIdConfig) 
                    ? openIdConfig.GetString() : null,
                ["loggerService"] = bootstrapJson.TryGetProperty("loggerService", out var loggerService) 
                    ? loggerService.GetString() : null,
                ["logLevel"] = bootstrapJson.TryGetProperty("logLevel", out var logLevel) 
                    ? logLevel.GetString() : "Information",
                ["allowedScopes"] = bootstrapJson.TryGetProperty("allowedScopes", out var allowedScopes) 
                    ? JsonSerializer.Deserialize<string[]>(allowedScopes.GetRawText()) : new[] { "openid", "profile", "email" },
                ["requiresRelay"] = bootstrapJson.TryGetProperty("requiresRelay", out var requiresRelay) 
                    && requiresRelay.GetBoolean()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error retrieving bootstrap configuration for appDomain={AppDomain}",
                appDomain);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific configuration value from user_config or service_config
    /// </summary>
    private async Task<IActionResult> GetConfigValueFromDb(
        string appDomain,
        string type,
        string configKey)
    {
        try
        {
            // Only return enabled entries for config requests
            var entry = await configService.GetByAppDomainAsync(appDomain, enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "Configuration entry not found or disabled for appDomain={AppDomain}",
                    appDomain);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration not found for appDomain '{appDomain}'"
                });
            }

            var value = entry.GetConfigValue(type, configKey);

            if (value is null)
            {
                logger.LogWarning(
                    "Configuration key '{ConfigKey}' not found in {Type}_config for appDomain={AppDomain}",
                    configKey, type, appDomain);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration key '{configKey}' not found in {type}_config"
                });
            }

            logger.LogInformation(
                "Returning {Type} config value for key={ConfigKey}, appDomain={AppDomain}",
                type, configKey, appDomain);

            return Ok(value.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error retrieving configuration for key={ConfigKey}, type={Type}, appDomain={AppDomain}",
                configKey, type, appDomain);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}