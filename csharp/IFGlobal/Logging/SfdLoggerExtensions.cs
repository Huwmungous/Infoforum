using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IFGlobal.Logging;

public static class SfdLoggerExtensions
{
    /// <summary>
    /// Adds the SfD Logger provider to the logging builder
    /// </summary>
    public static ILoggingBuilder AddSfdLogger(
        this ILoggingBuilder builder,
        SfdLoggerConfiguration configuration)
    {
        builder.Services.AddSingleton<ILoggerProvider>(
            new SfdLoggerProvider(
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
    public static ILoggingBuilder AddSfdLogger(
        this ILoggingBuilder builder,
        Action<SfdLoggerConfiguration> configure)
    {
        var configuration = new SfdLoggerConfiguration();
        configure(configuration);

        return builder.AddSfdLogger(configuration);
    }
}
