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
    /// <param name="realm">Realm name</param>
    /// <param name="client">Client base name</param>
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
        [FromQuery] string realm,
        [FromQuery] string client)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(cfg))
            return BadRequest(new ErrorResponse { Error = "Parameter 'cfg' is required" });

        if (string.IsNullOrWhiteSpace(type))
            return BadRequest(new ErrorResponse { Error = "Parameter 'type' is required" });

        if (string.IsNullOrWhiteSpace(realm))
            return BadRequest(new ErrorResponse { Error = "Parameter 'realm' is required" });

        if (string.IsNullOrWhiteSpace(client))
            return BadRequest(new ErrorResponse { Error = "Parameter 'client' is required" });

        // Normalise to lowercase for case-insensitive comparison
        cfg = cfg.ToLowerInvariant();
        type = type.ToLowerInvariant();

        if (type != "user" && type != "service")
            return BadRequest(new ErrorResponse { Error = "Parameter 'type' must be 'user' or 'service'" });

        // If cfg is 'bootstrap', allow unauthenticated access
        if (cfg == "bootstrap")
        {
            return await GetBootstrapConfigFromDb(realm, client, type);
        }

        // For all other cfg values, require authentication
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        // Query the database for the configuration
        return await GetConfigValueFromDb(realm, client, type, cfg);
    }

    /// <summary>
    /// Get Bootstrap configuration from database
    /// Returns a transformed object with clientId based on the app type
    /// </summary>
    private async Task<IActionResult> GetBootstrapConfigFromDb(string realm, string client, string type)
    {
        try
        {
            // Only return enabled entries for config requests
            var entry = await configService.GetAsync(realm, client, enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "Configuration entry not found or disabled for realm={Realm}, client={Client}",
                    realm, client);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration not found for realm '{realm}' and client '{client}'"
                });
            }

            if (entry.BootstrapConfig is null)
            {
                logger.LogWarning(
                    "Bootstrap config is null for realm={Realm}, client={Client}",
                    realm, client);
                return NotFound(new ErrorResponse
                {
                    Error = $"Bootstrap configuration not set for realm '{realm}' and client '{client}'"
                });
            }

            logger.LogInformation(
                "Returning bootstrap config for realm={Realm}, client={Client}, type={Type}",
                realm, client, type);

            // Transform the bootstrap config to return the appropriate values
            var bootstrapJson = entry.BootstrapConfig.RootElement;
            
            // Get clientId from JSONB
            string? clientId = bootstrapJson.TryGetProperty("clientId", out var clientIdElement)
                ? clientIdElement.GetString() : null;
            
            // Build the response object
            var response = new Dictionary<string, object?>
            {
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
                "Error retrieving bootstrap configuration for realm={Realm}, client={Client}",
                realm, client);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific configuration value from user_config or service_config
    /// </summary>
    private async Task<IActionResult> GetConfigValueFromDb(
        string realm,
        string client,
        string type,
        string configKey)
    {
        try
        {
            // Only return enabled entries for config requests
            var entry = await configService.GetAsync(realm, client, enabledOnly: true);

            if (entry is null)
            {
                logger.LogWarning(
                    "Configuration entry not found or disabled for realm={Realm}, client={Client}",
                    realm, client);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration not found for realm '{realm}' and client '{client}'"
                });
            }

            var value = entry.GetConfigValue(type, configKey);

            if (value is null)
            {
                logger.LogWarning(
                    "Configuration key '{ConfigKey}' not found in {Type}_config for realm={Realm}, client={Client}",
                    configKey, type, realm, client);
                return NotFound(new ErrorResponse
                {
                    Error = $"Configuration key '{configKey}' not found in {type}_config"
                });
            }

            logger.LogInformation(
                "Returning {Type} config value for key={ConfigKey}, realm={Realm}, client={Client}",
                type, configKey, realm, client);

            return Ok(value.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error retrieving configuration for key={ConfigKey}, type={Type}, realm={Realm}, client={Client}",
                configKey, type, realm, client);
            return StatusCode(500, new ErrorResponse { Error = "Internal server error" });
        }
    }
}