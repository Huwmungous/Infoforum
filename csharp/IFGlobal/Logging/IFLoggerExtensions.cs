using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IFGlobal.Logging;

public static class IFLoggerExtensions
{
    /// <summary>
    /// Adds the SfD Logger provider to the logging builder
    /// </summary>
    public static ILoggingBuilder AddIFLogger(
        this ILoggingBuilder builder,
        IFLoggerConfiguration configuration)
    {
        builder.Services.AddSingleton<ILoggerProvider>(
            new IFLoggerProvider(
                configuration.LoggerServiceUrl,
                configuration.ClientId,
                configuration.Realm,
                configuration.ApplicationName,
                configuration.EnvironmentName));

        // Set minimum log level if specified
        builder.SetMinimumLevel(configuration.MinimumLogLevel);

        return builder;
    }

    /// <summary>
    /// Adds the SfD Logger provider with a configuration action
    /// </summary>
    public static ILoggingBuilder AddIFLogger(
        this ILoggingBuilder builder,
        Action<IFLoggerConfiguration> configure)
    {
        var configuration = new IFLoggerConfiguration();
        configure(configuration);

        return builder.AddIFLogger(configuration);
    }
}
