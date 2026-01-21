using ConfigWebService.Entities;
using ConfigWebService.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConfigWebService.Controllers;

[ApiController]
[Route("[controller]")]
public class ConfigDbController : ControllerBase
{
    private readonly ConfigService _service;

    public ConfigDbController(ConfigService service)
    {
        _service = service;
    }

    // -----------------------------
    // GET ENDPOINTS
    // -----------------------------

    [HttpGet("batch")]
    public async Task<IActionResult> GetBatch(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 100)
    {
        var result = await _service.GetBatchAsync(offset, limit);
        return Ok(result);
    }

    [HttpGet("{realm}/{client}")]
    public async Task<IActionResult> Get(string realm, string client)
    {
        var result = await _service.GetAsync(realm, client);
        return result is null ? NotFound() : Ok(result);
    }

    // -----------------------------
    // POST ENDPOINTS
    // -----------------------------

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConfigEntry config)
    {
        if (config is null)
            return BadRequest();

        var created = await _service.CreateAsync(config);
        return CreatedAtAction(
            nameof(Get),
            new { realm = created.Realm, client = created.Client },
            created
        );
    }

    [HttpPut("{realm}/{client}")]
    public async Task<IActionResult> Update(
        string realm,
        string client,
        [FromBody] ConfigEntry config)
    {
        if (config is null)
            return BadRequest();

        var updated = await _service.UpdateAsync(realm, client, config);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{realm}/{client}")]
    public async Task<IActionResult> Delete(string realm, string client)
    {
        var deleted = await _service.DeleteAsync(realm, client);
        return deleted ? NoContent() : NotFound();
    }
}
