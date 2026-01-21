using IFGlobal.Config;
using IFGlobal.DataAccess;
using Microsoft.AspNetCore.Mvc; 

namespace SampleWebService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SampleController : ControllerBase
{
    private readonly ILogger<SampleController> _logger;
    private readonly IBaseRepository _dataAccess;
    private readonly IConfigProvider _configProvider;

    public SampleController(
        ILogger<SampleController> logger,
        IBaseRepository dataAccess,
        IConfigProvider configProvider)
    {
        _logger = logger;
        _dataAccess = dataAccess;
        _configProvider = configProvider;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        _logger.LogInformation("Health check requested");

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            message = "Sample Web Service is running",
            dataAccessMode = _dataAccess.Mode.ToString()
        });
    }

    /// <summary>
    /// Get current data access mode information
    /// </summary>
    [HttpGet("mode")]
    public IActionResult GetMode()
    {
        return Ok(new
        {
            mode = _dataAccess.Mode.ToString(),
            isRelay = _dataAccess.Mode == DataAccessMode.Relay
        });
    }

    /// <summary>
    /// Test database connectivity
    /// </summary>
    [HttpGet("ping-db")]
    public async Task<IActionResult> PingDatabase(CancellationToken cancellationToken)
    {
        try
        {
            int result;
            using (var connection = _dataAccess.GetConnection())
            {
                await connection.OpenAsync(cancellationToken);
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1";
                    var scalar = await command.ExecuteScalarAsync(cancellationToken);
                    result = Convert.ToInt32(scalar);
                }
            }

            return Ok(new
            {
                success = true,
                mode = _dataAccess.Mode.ToString(),
                message = "Database connection successful"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database ping failed");
            return StatusCode(503, new
            {
                success = false,
                mode = _dataAccess.Mode.ToString(),
                message = $"Database connection failed: {ex.Message}"
            });
        }
    }
}