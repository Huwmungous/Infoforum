using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SampleWebService.Models;
using SampleWebService.Repositories;

namespace SampleWebService.Controllers;

/// <summary>
/// API controller for Coach operations.
/// Demonstrates controller patterns using IFGlobal infrastructure.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoachController(
    CoachRepository repository,
    ILogger<CoachController> logger) : ControllerBase
{
    /// <summary>
    /// Get all coaches.
    /// </summary>
    /// <returns>List of all coaches ordered by idx.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Coach>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting all coaches");
            var coaches = await repository.GetAllAsync(cancellationToken);
            return Ok(coaches);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving coaches");
            return StatusCode(500, new { error = "Failed to retrieve coaches" });
        }
    }

    /// <summary>
    /// Get a coach by idx.
    /// </summary>
    /// <param name="id">The coach idx.</param>
    /// <returns>The coach if found.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Coach), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting coach with idx {Idx}", id);
            var coach = await repository.GetByIdAsync(id, cancellationToken);
            
            if (coach is null)
            {
                logger.LogWarning("Coach with idx {Idx} not found", id);
                return NotFound(new { error = $"Coach with idx {id} not found" });
            }
            
            return Ok(coach);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving coach {Idx}", id);
            return StatusCode(500, new { error = "Failed to retrieve coach" });
        }
    }

    /// <summary>
    /// Get the total count of coaches.
    /// </summary>
    /// <returns>The count of coaches.</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCount(CancellationToken cancellationToken)
    {
        try
        {
            var count = await repository.GetCountAsync(cancellationToken);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting coach count");
            return StatusCode(500, new { error = "Failed to get coach count" });
        }
    }
}
