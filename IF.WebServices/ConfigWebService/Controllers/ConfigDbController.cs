using ConfigWebService.Entities;
using ConfigWebService.Services;
using ConfigWebService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ConfigWebService.Controllers;

/// <summary>
/// Admin controller for managing configuration entries
/// </summary>
[ApiController]
[Route("[controller]")]
public class ConfigDbController(ConfigService service, ILogger<ConfigDbController> logger) : ControllerBase
{
    // -----------------------------
    // GET ENDPOINTS
    // -----------------------------

    /// <summary>
    /// Get a paginated batch of configuration entries (includes disabled)
    /// </summary>
    [HttpGet("batch")]
    public async Task<IActionResult> GetBatch(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100,
        [FromQuery] bool includeDisabled = true)
    {
        var entries = await service.GetBatchAsync(offset, limit, includeDisabled);
        var total = await service.GetCountAsync(includeDisabled);

        return Ok(new
        {
            entries,
            total,
            offset,
            limit
        });
    }

    /// <summary>
    /// Get a configuration entry by idx
    /// </summary>
    [HttpGet("{idx:int}")]
    public async Task<IActionResult> GetByIdx(int idx)
    {
        var result = await service.GetByIdxAsync(idx);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Get a configuration entry by app domain
    /// </summary>
    [HttpGet("{appDomain}")]
    public async Task<IActionResult> Get(string appDomain)
    {
        var result = await service.GetByAppDomainAsync(appDomain, enabledOnly: false);
        return result is null ? NotFound() : Ok(result);
    }

    // -----------------------------
    // POST ENDPOINTS
    // -----------------------------

    /// <summary>
    /// Create a new configuration entry
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConfigEntryDto config)
    {
        if (config is null)
            return BadRequest(new ErrorResponse { Error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(config.AppDomain))
            return BadRequest(new ErrorResponse { Error = "AppDomain is required" });

        // Check for existing entry
        var existing = await service.GetByAppDomainAsync(config.AppDomain, enabledOnly: false);
        if (existing is not null)
            return Conflict(new ErrorResponse { Error = $"Entry already exists for appDomain '{config.AppDomain}'" });

        var entry = new ConfigEntry
        {
            AppDomain = config.AppDomain,
            UserConfig = ParseJsonDocument(config.UserConfig),
            ServiceConfig = ParseJsonDocument(config.ServiceConfig),
            BootstrapConfig = BuildBootstrapConfig(config.BootstrapConfig, !config.Enabled)
        };

        var created = await service.CreateAsync(entry);

        logger.LogInformation(
            "Created config entry idx={Idx} for appDomain={AppDomain}",
            created.Idx, created.AppDomain);

        return CreatedAtAction(
            nameof(GetByIdx),
            new { idx = created.Idx },
            created
        );
    }

    // -----------------------------
    // PUT ENDPOINTS
    // -----------------------------

    /// <summary>
    /// Update an existing configuration entry by idx
    /// </summary>
    [HttpPut("{idx:int}")]
    public async Task<IActionResult> UpdateByIdx(int idx, [FromBody] ConfigEntryDto config)
    {
        if (config is null)
            return BadRequest(new ErrorResponse { Error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(config.AppDomain))
            return BadRequest(new ErrorResponse { Error = "AppDomain is required" });

        var entry = new ConfigEntry
        {
            Idx = idx,
            AppDomain = config.AppDomain,
            UserConfig = ParseJsonDocument(config.UserConfig),
            ServiceConfig = ParseJsonDocument(config.ServiceConfig),
            BootstrapConfig = BuildBootstrapConfig(config.BootstrapConfig, !config.Enabled)
        };

        var updated = await service.UpdateByIdxAsync(idx, entry);

        if (!updated)
            return NotFound(new ErrorResponse { Error = $"Entry with idx {idx} not found" });

        logger.LogInformation(
            "Updated config entry idx={Idx} for appDomain={AppDomain}",
            idx, config.AppDomain);

        return NoContent();
    }

    /// <summary>
    /// Update an existing configuration entry by app domain
    /// </summary>
    [HttpPut("{appDomain}")]
    public async Task<IActionResult> Update(
        string appDomain,
        [FromBody] ConfigEntryDto config)
    {
        if (config is null)
            return BadRequest(new ErrorResponse { Error = "Request body is required" });

        var entry = new ConfigEntry
        {
            AppDomain = config.AppDomain ?? appDomain,
            UserConfig = ParseJsonDocument(config.UserConfig),
            ServiceConfig = ParseJsonDocument(config.ServiceConfig),
            BootstrapConfig = BuildBootstrapConfig(config.BootstrapConfig, !config.Enabled)
        };

        var updated = await service.UpdateByAppDomainAsync(appDomain, entry);

        if (!updated)
            return NotFound(new ErrorResponse { Error = $"Entry not found for appDomain '{appDomain}'" });

        logger.LogInformation(
            "Updated config entry for appDomain={AppDomain}",
            appDomain);

        return NoContent();
    }

    // -----------------------------
    // PATCH ENDPOINTS
    // -----------------------------

    /// <summary>
    /// Enable or disable a configuration entry
    /// </summary>
    [HttpPatch("{idx:int}/enabled")]
    public async Task<IActionResult> SetEnabled(int idx, [FromBody] EnabledDto dto)
    {
        if (dto is null)
            return BadRequest(new ErrorResponse { Error = "Request body is required" });

        var success = await service.SetEnabledAsync(idx, dto.Enabled);

        if (!success)
            return NotFound(new ErrorResponse { Error = $"Entry with idx {idx} not found" });

        logger.LogInformation(
            "Set enabled={Enabled} for config entry idx={Idx}",
            dto.Enabled, idx);

        return NoContent();
    }

    // -----------------------------
    // DELETE ENDPOINTS
    // -----------------------------

    /// <summary>
    /// Delete a configuration entry by idx
    /// </summary>
    [HttpDelete("{idx:int}")]
    public async Task<IActionResult> DeleteByIdx(int idx)
    {
        var deleted = await service.DeleteByIdxAsync(idx);

        if (!deleted)
            return NotFound(new ErrorResponse { Error = $"Entry with idx {idx} not found" });

        logger.LogInformation("Deleted config entry idx={Idx}", idx);

        return NoContent();
    }

    /// <summary>
    /// Delete a configuration entry by app domain
    /// </summary>
    [HttpDelete("{appDomain}")]
    public async Task<IActionResult> Delete(string appDomain)
    {
        var deleted = await service.DeleteByAppDomainAsync(appDomain);

        if (!deleted)
            return NotFound(new ErrorResponse { Error = $"Entry not found for appDomain '{appDomain}'" });

        logger.LogInformation(
            "Deleted config entry for appDomain={AppDomain}",
            appDomain);

        return NoContent();
    }

    // -----------------------------
    // HELPERS
    // -----------------------------

    private static JsonDocument? ParseJsonDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Build bootstrap config JSONB, optionally adding "disabled" property
    /// </summary>
    private static JsonDocument? BuildBootstrapConfig(string? existingJson, bool disabled)
    {
        var dict = new Dictionary<string, object>();

        // Parse existing JSON if provided
        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(existingJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name != "disabled") // Don't copy disabled, we'll set it based on parameter
                        dict[prop.Name] = prop.Value.Clone();
                }
            }
            catch (JsonException)
            {
                // Invalid JSON, start fresh
            }
        }

        // Set disabled if requested
        if (disabled)
            dict["disabled"] = true;
        else
            dict.Remove("disabled"); // Ensure it's not present if enabled

        if (dict.Count == 0)
            return null;

        var json = JsonSerializer.Serialize(dict);
        return JsonDocument.Parse(json);
    }
}

/// <summary>
/// DTO for creating/updating configuration entries
/// </summary>
public class ConfigEntryDto
{
    public string? AppDomain { get; set; }
    public string? UserConfig { get; set; }
    public string? ServiceConfig { get; set; }
    public string? BootstrapConfig { get; set; }
    /// <summary>
    /// If false, adds "disabled": true to bootstrap_config JSONB
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// DTO for enabling/disabling entries
/// </summary>
public class EnabledDto
{
    public bool Enabled { get; set; }
}
