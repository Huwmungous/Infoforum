using Microsoft.Extensions.DependencyInjection;
using IFGlobal.Configuration;
using IFGlobal.Controllers;

namespace IFGlobal.Extensions;

/// <summary>
/// Extension methods for configuring health endpoints.
/// </summary>
public static class HealthControllerExtensions
{
    /// <summary>
    /// Adds the SfD health controller configuration to the service collection.
    /// The HealthController itself will be discovered automatically when AddControllers() is called,
    /// as long as the IFGlobal assembly is referenced.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">The name of the service to include in health responses.</param>
    /// <param name="serviceVersion">Optional version string to include in health responses.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSfdHealthController(
        this IServiceCollection services,
        string serviceName,
        string? serviceVersion = null)
    {
        services.Configure<HealthControllerOptions>(options =>
        {
            options.ServiceName = serviceName;
            options.ServiceVersion = serviceVersion;
        });

        return services;
    }

    /// <summary>
    /// Adds the SfD health controller configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure health controller options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSfdHealthController(
        this IServiceCollection services,
        Action<HealthControllerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services;
    }

    /// <summary>
    /// Adds controllers from the IFGlobal assembly.
    /// Call this if the HealthController is not being auto-discovered.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>The MVC builder for chaining.</returns>
    public static IMvcBuilder AddSfdCommonControllers(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(typeof(HealthController).Assembly);
    }
}
