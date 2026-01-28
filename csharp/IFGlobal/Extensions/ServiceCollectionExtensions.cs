using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IFGlobal.Configuration;
using IFGlobal.Logging;
using IFGlobal.Services;

namespace IFGlobal.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SfD configuration and logging services to the service collection.
    /// This will fetch bootstrap configuration and configure logging accordingly.
    /// </summary>
    public static IServiceCollection AddSfdCommon(
        this IServiceCollection services,
        IConfiguration configuration,
        string applicationName,
        string environmentName = "")
    {
        // Register IFConfiguration
        services.Configure<IFConfiguration>(
            configuration.GetSection(IFConfiguration.SectionName));

        // Register HttpClientFactory
        services.AddHttpClient();
        services.AddHttpClient("IFLogger")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

        // Register ConfigService
        services.AddSingleton<IConfigService, ConfigService>();

        // Add IFLogger to logging pipeline
        services.AddLogging(logging =>
        {
            logging.AddIFLogger(configuration, applicationName, environmentName);
        });

        return services;
    }

    /// <summary>
    /// Adds SfD logger provider to the logging builder.
    /// </summary>
    public static ILoggingBuilder AddIFLogger(
        this ILoggingBuilder builder,
        IConfiguration configuration,
        string applicationName,
        string environmentName = "")
    {
        // Register logger configuration
        builder.Services.Configure<IFLoggerConfiguration>(
            configuration.GetSection(IFLoggerConfiguration.SectionName));

        // Register the logger provider
        builder.Services.AddSingleton<ILoggerProvider>(serviceProvider =>
        {
            var config = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IFLoggerConfiguration>>();
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var configService = serviceProvider.GetRequiredService<IConfigService>();

            // Use ConfigService values if available, otherwise use appsettings values
            var realm = configService.IsInitialized ? configService.Realm : "Unknown";
            var client = configService.IsInitialized ? configService.ClientId : "Unknown";

            return new IFLoggerProvider(
                config,
                httpClientFactory,
                realm,
                client,
                applicationName,
                environmentName);
        });

        return builder;
    }

    /// <summary>
    /// Adds the ConfigInitializationService to ensure ConfigService is initialized on startup.
    /// </summary>
    public static IServiceCollection AddSfdConfigInitialization(this IServiceCollection services)
    {
        services.AddHostedService<ConfigInitializationService>();
        return services;
    }

    /// <summary>
    /// Configures SfD logger with values from ConfigService after bootstrap initialization.
    /// Call this after ConfigService.InitializeAsync() completes.
    /// </summary>
    public static IServiceCollection ConfigureIFLoggerFromBootstrap(
        this IServiceCollection services,
        IConfigService configService)
    {
        services.Configure<IFLoggerConfiguration>(options =>
        {
            if (!string.IsNullOrEmpty(configService.LoggerService))
            {
                options.LoggerService = configService.LoggerService;
                options.EnableRemoteLogging = true;
            }
            else
            {
                options.EnableRemoteLogging = false;
            }

            options.MinimumLogLevel = configService.LogLevel;
        });

        return services;
    }
}
