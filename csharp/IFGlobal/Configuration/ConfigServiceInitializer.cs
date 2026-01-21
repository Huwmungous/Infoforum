using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IFGlobal.Configuration;

/// <summary>
/// Hosted service to initialize ConfigService at startup.
/// </summary>
public sealed class ConfigServiceInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ConfigServiceInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Resolve ConfigService to trigger initialization
        using var scope = _serviceProvider.CreateScope();
        var configService = scope.ServiceProvider.GetService<ConfigService>();
        if (configService is IAsyncInitializable asyncInitializable)
        {
            await asyncInitializable.InitializeAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup required
        return Task.CompletedTask;
    }
}

/// <summary>
/// Optional interface for async initialization.
/// </summary>
public interface IAsyncInitializable
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
