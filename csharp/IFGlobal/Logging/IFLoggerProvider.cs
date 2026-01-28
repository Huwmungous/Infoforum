using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IFGlobal.Logging;

/// <summary>
/// Logger provider for the SfD logging service.
/// </summary>
public class IFLoggerProvider : ILoggerProvider
{
    private readonly IOptions<IFLoggerConfiguration>? _configOptions;
    private readonly IFLoggerConfiguration? _directConfig;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly string _realm;
    private readonly string _clientId;
    private readonly string? _applicationName;
    private readonly string? _environmentName;

    /// <summary>
    /// Creates a provider with IHttpClientFactory (recommended for ASP.NET Core).
    /// </summary>
    public IFLoggerProvider(
        IOptions<IFLoggerConfiguration> config,
        IHttpClientFactory httpClientFactory,
        string realm,
        string clientId,
        string applicationName,
        string environmentName)
    {
        _configOptions = config;
        _httpClientFactory = httpClientFactory;
        _realm = realm;
        _clientId = clientId;
        _applicationName = applicationName;
        _environmentName = environmentName;
    }

    /// <summary>
    /// Creates a provider with full configuration (for standalone/CLI applications).
    /// </summary>
    public IFLoggerProvider(IFLoggerConfiguration config)
    {
        _directConfig = config;
        _clientId = config.ClientId;
        _realm = config.Realm;
        _applicationName = config.ApplicationName;
        _environmentName = config.EnvironmentName;
    }

    /// <summary>
    /// Creates a provider with explicit configuration (legacy constructor).
    /// </summary>
    public IFLoggerProvider(string loggerServiceUrl, string clientId, string realm, string applicationName = "", string environmentName = "")
    {
        _directConfig = new IFLoggerConfiguration
        {
            LoggerServiceUrl = loggerServiceUrl,
            ClientId = clientId,
            Realm = realm,
            ApplicationName = applicationName,
            EnvironmentName = environmentName
        };
        _clientId = clientId;
        _realm = realm;
        _applicationName = applicationName;
        _environmentName = environmentName;
    }
        
    public ILogger CreateLogger(string categoryName)
    {
        if (_httpClientFactory != null && _configOptions != null && _applicationName != null)
        {
            return new IFLogger(
                categoryName,
                _httpClientFactory,
                _configOptions.Value,
                _realm,
                _clientId,
                _applicationName,
                _environmentName ?? string.Empty);
        }
        else if (_directConfig != null)
        {
            return new IFLogger(categoryName, _directConfig);
        }
        else
        {
            throw new InvalidOperationException("IFLoggerProvider not properly configured");
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
