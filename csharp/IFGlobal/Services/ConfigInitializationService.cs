using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IFGlobal.Configuration;

namespace IFGlobal.Services;

/// <summary>
/// Background service that initializes ConfigService on application startup.
/// This ensures configuration is loaded before the application starts processing requests.
/// </summary>
public class ConfigInitializationService : IHostedService
{
    private readonly IConfigService _configService;
    private readonly ILogger<ConfigInitializationService> _logger;

    public ConfigInitializationService(
        IConfigService configService,
        ILogger<ConfigInitializationService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing ConfigService...");
            await _configService.InitializeAsync(cancellationToken);
            _logger.LogInformation("ConfigService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize ConfigService. Application may not function correctly.");
            throw; // Re-throw to prevent app from starting if config is critical
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
