using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IFGlobal.Configuration;

public partial class ConfigService(
    IOptions<IFConfiguration> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ConfigService> logger) : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IFConfiguration _config = options.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<ConfigService> _logger = logger;
    private BootstrapConfig? _bootstrapConfig;
    private bool _initialized;
    private DateTime? _bootstrapFetchedAt;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Returns the appropriate ClientId for JWT audience validation based on AppType.
    /// For Patient apps, returns "{client}-pps" (e.g., "dev-login-pps").
    /// For other apps, returns the standard service ClientId from bootstrap config.
    /// </summary>
    public string ClientId
    {
        get
        {
            if (_bootstrapConfig == null)
                throw new InvalidOperationException("ConfigService not initialized. Call InitializeAsync first.");

            return _bootstrapConfig.ClientId;
        }
    }

    /// <summary>
    /// Returns the service ClientId for service-to-service authentication.
    /// Always returns the bootstrap ClientId (e.g., "dev-login-svc") regardless of AppType.
    /// </summary>
    public string ServiceClientId => _bootstrapConfig?.ClientId
        ?? throw new InvalidOperationException("ConfigService not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Returns the client secret for service authentication.
    /// Comes from appsettings.json or IF_CLIENTSECRET environment variable.
    /// Only required for Service AppType.
    /// </summary>
    public string? ClientSecret => _config.ClientSecret;

    public string OpenIdConfig => _bootstrapConfig?.OpenIdConfig
        ?? throw new InvalidOperationException("ConfigService not initialized. Call InitializeAsync first.");

    public string? LoggerService => _bootstrapConfig?.LoggerService;

    public LogLevel LogLevel => ParseLogLevel(_bootstrapConfig?.LogLevel ?? "Information");

    /// <summary>
    /// Realm from bootstrap config (e.g., 'LongmanRd')
    /// </summary>
    public string Realm => _bootstrapConfig?.Realm
        ?? throw new InvalidOperationException("ConfigService not initialized. Call InitializeAsync first.");

    /// <summary>
    /// AppDomain from configuration (e.g., 'Infoforum', 'BreakTackle')
    /// </summary>
    public string AppDomain => _config.AppDomain;

    public bool IsInitialized => _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: if already initialized, return cached config
        if (_initialized)
        {
            LogReturningCachedBootstrapConfig(_logger, _bootstrapFetchedAt!.Value);
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_initialized)
            {
                LogReturningCachedBootstrapConfig(_logger, _bootstrapFetchedAt!.Value);
                return;
            }

            // Bootstrap always uses "service" type - the service needs service credentials
            // to fetch configuration. AppType only affects the ClientId for JWT validation.
            // Include ServiceName for diagnostic logging on ConfigWebService.
            var serviceName = Uri.EscapeDataString(_config.ServiceName);
            var url = $"{_config.ConfigService}/Config?cfg=bootstrap&type=service&appDomain={_config.AppDomain}&app={serviceName}";

            LogFetchingBootstrapConfigFromService(_logger, url);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                   $"Failed to fetch bootstrap configuration: {response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _bootstrapConfig = JsonSerializer.Deserialize<BootstrapConfig>(content, JsonOptions);

            if (_bootstrapConfig == null ||
                string.IsNullOrEmpty(_bootstrapConfig.Realm) ||
                string.IsNullOrEmpty(_bootstrapConfig.ClientId) ||
                string.IsNullOrEmpty(_bootstrapConfig.OpenIdConfig))
            {
                throw new InvalidOperationException("Invalid bootstrap configuration: missing realm, clientId or openIdConfig");
            }

            // Append realm to OpenIdConfig
            _bootstrapConfig.OpenIdConfig = $"{_bootstrapConfig.OpenIdConfig}/{_bootstrapConfig.Realm}";

            _bootstrapFetchedAt = DateTime.UtcNow;

            LogBootstrapConfigLoaded(_logger, _bootstrapFetchedAt.Value);
            LogServiceClientId(_logger, ServiceClientId);
            LogClientId(_logger, ClientId);
            LogAuthority(_logger, _bootstrapConfig.OpenIdConfig);
            LogAppType(_logger, _config.AppType);

            if (!string.IsNullOrEmpty(_bootstrapConfig.LoggerService))
            {
                LogLoggerServiceUrl(_logger, _bootstrapConfig.LoggerService);
            }

            LogLogLevel(_logger, _bootstrapConfig.LogLevel);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static LogLevel ParseLogLevel(string level)
    {
        // Handle numeric levels (0-5)
        if (int.TryParse(level, out var numLevel))
        {
            return numLevel switch
            {
                0 => LogLevel.Trace,
                1 => LogLevel.Debug,
                2 => LogLevel.Information,
                3 => LogLevel.Warning,
                4 => LogLevel.Error,
                5 => LogLevel.Critical,
                _ => LogLevel.Information
            };
        }

        // Handle string levels
        return level.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information
        };
    }

    /// <summary>
    /// Fetches authenticated configuration from the config service.
    /// </summary>
    /// <typeparam name="T">The type to deserialise the configuration into</typeparam>
    /// <param name="configName">The configuration key (e.g., "firebirddb", "loggerdb")</param>
    /// <param name="accessToken">Bearer token for authentication</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The deserialised configuration object, or null if not found</returns>
    public async Task<T?> GetConfigAsync<T>(string configName, string accessToken, CancellationToken cancellationToken = default) where T : class
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("ConfigService not initialised. Call InitializeAsync() first.");
        }

        if (string.IsNullOrWhiteSpace(configName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty.", nameof(configName));
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));
        }

        try
        {
            // Config fetches always use "service" type - we're authenticated as a service
            // Include ServiceName for diagnostic logging on ConfigWebService.
            var serviceName = Uri.EscapeDataString(_config.ServiceName);
            var url = $"{_config.ConfigService}/Config?cfg={configName}&type=service&appDomain={_config.AppDomain}&app={serviceName}";

            LogFetchingConfigFromService(_logger, configName, url);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LogConfigFetchFailed(_logger, configName, (int)response.StatusCode, response.ReasonPhrase, errorContent);

                throw new HttpRequestException(
                    $"Failed to fetch configuration '{configName}': {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            LogConfigReceived(_logger, configName, content.Length);

            var config = JsonSerializer.Deserialize<T>(content, JsonOptions);

            if (config == null)
            {
                LogConfigDeserializedNull(_logger, configName);
            }

            return config;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogConfigFetchError(_logger, ex, configName);
            throw;
        }
    }

    // ============================================================================
    // LoggerMessage source generators for high-performance logging
    // ============================================================================

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Returning CACHED bootstrap config (fetched at {FetchedAt:yyyy-MM-dd HH:mm:ss} UTC) - NOT calling ConfigWebService")]
    private static partial void LogReturningCachedBootstrapConfig(ILogger logger, DateTime fetchedAt);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "CALLING ConfigWebService to fetch bootstrap configuration from: {Url}")]
    private static partial void LogFetchingBootstrapConfigFromService(ILogger logger, string url);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Bootstrap configuration loaded successfully from ConfigWebService at {FetchedAt:yyyy-MM-dd HH:mm:ss} UTC")]
    private static partial void LogBootstrapConfigLoaded(ILogger logger, DateTime fetchedAt);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "ServiceClientId: {ServiceClientId}")]
    private static partial void LogServiceClientId(ILogger logger, string serviceClientId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "ClientId (audience): {ClientId}")]
    private static partial void LogClientId(ILogger logger, string clientId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Authority: {Authority}")]
    private static partial void LogAuthority(ILogger logger, string authority);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "AppType: {AppType}")]
    private static partial void LogAppType(ILogger logger, AuthType appType);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Logger Service URL: {LoggerService}")]
    private static partial void LogLoggerServiceUrl(ILogger logger, string loggerService);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Log Level: {LogLevel}")]
    private static partial void LogLogLevel(ILogger logger, string logLevel);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "CALLING ConfigWebService to fetch configuration '{ConfigName}' from: {Url}")]
    private static partial void LogFetchingConfigFromService(ILogger logger, string configName, string url);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to fetch configuration '{ConfigName}': {StatusCode} {ReasonPhrase}. Response: {ErrorContent}")]
    private static partial void LogConfigFetchFailed(ILogger logger, string configName, int statusCode, string? reasonPhrase, string errorContent);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Received configuration '{ConfigName}': {ContentLength} bytes")]
    private static partial void LogConfigReceived(ILogger logger, string configName, int contentLength);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Configuration '{ConfigName}' deserialised to null")]
    private static partial void LogConfigDeserializedNull(ILogger logger, string configName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Error fetching configuration '{ConfigName}'")]
    private static partial void LogConfigFetchError(ILogger logger, Exception ex, string configName);
}