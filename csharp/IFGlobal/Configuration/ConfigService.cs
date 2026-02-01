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

    /// <summary>
    /// Default cache duration for configuration values (24 hours).
    /// </summary>
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromHours(24);

    private readonly IFConfiguration _config = options.Value;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<ConfigService> _logger = logger;
    private BootstrapConfig? _bootstrapConfig;
    private bool _initialized;
    private DateTime? _bootstrapFetchedAt;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Configuration cache with thread-safe access
    private readonly Dictionary<string, ConfigCacheEntry> _configCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// Represents a cached configuration entry with expiry time.
    /// </summary>
    private sealed class ConfigCacheEntry
    {
        public required string JsonContent { get; init; }
        public required DateTime FetchedAt { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }

    /// <summary>
    /// Returns the appropriate ClientId for JWT audience validation based on AppType.
    /// </summary>
    public string ClientId
    {
        get
        {
            if(_bootstrapConfig == null)
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

    /// <summary>
    /// Maximum number of retry attempts for bootstrap configuration fetch.
    /// </summary>
    private const int MaxBootstrapRetries = 5;

    /// <summary>
    /// Initial delay between retry attempts (doubles with each retry).
    /// </summary>
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(2);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: if already initialized, return cached config
        if(_initialized)
        {
            LogReturningCachedBootstrapConfig(_logger, _bootstrapFetchedAt!.Value);
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if(_initialized)
            {
                LogReturningCachedBootstrapConfig(_logger, _bootstrapFetchedAt!.Value);
                return;
            }

            // Bootstrap always uses "service" type - the service needs service credentials
            // to fetch configuration. AppType only affects the ClientId for JWT validation.
            // Include ServiceName for diagnostic logging on ConfigWebService.
            var serviceName = Uri.EscapeDataString(_config.ServiceName);
            var url = $"{_config.ConfigService}/Config?cfg=bootstrap&type=service&appDomain={_config.AppDomain}&app={serviceName}";

            // Retry with exponential backoff
            Exception? lastException = null;
            var delay = InitialRetryDelay;

            for(int attempt = 1; attempt <= MaxBootstrapRetries; attempt++)
            {
                try
                {
                    LogFetchingBootstrapConfigFromService(_logger, url);

                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(30); // Explicit timeout
                    var response = await httpClient.GetAsync(url, cancellationToken);

                    if(!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        throw new HttpRequestException(
                           $"Failed to fetch bootstrap configuration: {response.StatusCode} {response.ReasonPhrase}. Body: {errorBody}");
                    }

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    _bootstrapConfig = JsonSerializer.Deserialize<BootstrapConfig>(content, JsonOptions);

                    if(_bootstrapConfig == null ||
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

                    if(!string.IsNullOrEmpty(_bootstrapConfig.LoggerService))
                    {
                        LogLoggerServiceUrl(_logger, _bootstrapConfig.LoggerService);
                    }

                    LogLogLevel(_logger, _bootstrapConfig.LogLevel);

                    _initialized = true;
                    return; // Success - exit the retry loop
                }
                catch(Exception ex) when(ex is not OperationCanceledException)
                {
                    lastException = ex;

                    if(attempt < MaxBootstrapRetries)
                    {
                        LogBootstrapRetry(_logger, attempt, MaxBootstrapRetries, delay.TotalSeconds, ex.Message);
                        await Task.Delay(delay, cancellationToken);
                        delay *= 2; // Exponential backoff
                    }
                }
            }

            // All retries exhausted
            throw new InvalidOperationException(
                $"Failed to fetch bootstrap configuration after {MaxBootstrapRetries} attempts", lastException);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static LogLevel ParseLogLevel(string level)
    {
        // Handle numeric levels (0-5)
        if(int.TryParse(level, out var numLevel))
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
    /// Fetches authenticated configuration from the config service with caching.
    /// Cached values are returned for subsequent calls until they expire.
    /// </summary>
    /// <typeparam name="T">The type to deserialise the configuration into</typeparam>
    /// <param name="configName">The configuration key (e.g., "firebirddb", "loggerdb")</param>
    /// <param name="accessToken">Bearer token for authentication</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <param name="cacheDuration">Optional cache duration (defaults to 24 hours)</param>
    /// <returns>The deserialised configuration object, or null if not found</returns>
    public async Task<T?> GetConfigAsync<T>(
        string configName,
        string accessToken,
        CancellationToken cancellationToken = default,
        TimeSpan? cacheDuration = null) where T : class
    {
        if(!_initialized)
        {
            throw new InvalidOperationException("ConfigService not initialised. Call InitializeAsync() first.");
        }

        if(string.IsNullOrWhiteSpace(configName))
        {
            throw new ArgumentException("Configuration name cannot be null or empty.", nameof(configName));
        }

        if(string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));
        }

        var effectiveCacheDuration = cacheDuration ?? DefaultCacheDuration;
        var cacheKey = $"{_config.AppDomain}:{configName}";

        // Fast path: check cache without lock
        if(_configCache.TryGetValue(cacheKey, out var cachedEntry) && !cachedEntry.IsExpired)
        {
            LogReturningCachedConfig(_logger, configName, cachedEntry.FetchedAt);
            return JsonSerializer.Deserialize<T>(cachedEntry.JsonContent, JsonOptions);
        }

        // Need to fetch - acquire lock to prevent multiple concurrent fetches for the same config
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if(_configCache.TryGetValue(cacheKey, out cachedEntry) && !cachedEntry.IsExpired)
            {
                LogReturningCachedConfig(_logger, configName, cachedEntry.FetchedAt);
                return JsonSerializer.Deserialize<T>(cachedEntry.JsonContent, JsonOptions);
            }

            // Fetch from config service
            var content = await FetchConfigFromServiceAsync(configName, accessToken, cancellationToken);

            if(content == null)
            {
                return null;
            }

            // Cache the raw JSON content
            var now = DateTime.UtcNow;
            _configCache[cacheKey] = new ConfigCacheEntry
            {
                JsonContent = content,
                FetchedAt = now,
                ExpiresAt = now.Add(effectiveCacheDuration)
            };

            LogConfigCached(_logger, configName, effectiveCacheDuration.TotalHours);

            var config = JsonSerializer.Deserialize<T>(content, JsonOptions);

            if(config == null)
            {
                LogConfigDeserializedNull(_logger, configName);
            }

            return config;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Fetches configuration from the config service without caching.
    /// </summary>
    private async Task<string?> FetchConfigFromServiceAsync(
        string configName,
        string accessToken,
        CancellationToken cancellationToken)
    {
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

            if(!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LogConfigFetchFailed(_logger, configName, (int)response.StatusCode, response.ReasonPhrase, errorContent);

                throw new HttpRequestException(
                    $"Failed to fetch configuration '{configName}': {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            LogConfigReceived(_logger, configName, content.Length);

            return content;
        }
        catch(HttpRequestException)
        {
            throw;
        }
        catch(Exception ex)
        {
            LogConfigFetchError(_logger, ex, configName);
            throw;
        }
    }

    /// <summary>
    /// Invalidates a specific cached configuration, forcing the next request to fetch fresh data.
    /// </summary>
    /// <param name="configName">The configuration key to invalidate</param>
    /// <returns>True if the cache entry was found and removed, false otherwise</returns>
    public async Task<bool> InvalidateCacheAsync(string configName)
    {
        var cacheKey = $"{_config.AppDomain}:{configName}";

        await _cacheLock.WaitAsync();
        try
        {
            var removed = _configCache.Remove(cacheKey);
            if(removed)
            {
                LogConfigCacheInvalidated(_logger, configName);
            }
            return removed;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Clears all cached configuration values.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            var count = _configCache.Count;
            _configCache.Clear();
            LogConfigCacheCleared(_logger, count);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Gets information about the current cache state.
    /// </summary>
    public IReadOnlyDictionary<string, (DateTime FetchedAt, DateTime ExpiresAt, bool IsExpired)> GetCacheInfo()
    {
        return _configCache.ToDictionary(
            kvp => kvp.Key,
            kvp => (kvp.Value.FetchedAt, kvp.Value.ExpiresAt, kvp.Value.IsExpired));
    }

    // ============================================================================
    // LoggerMessage source generators for high-performance logging
    // ============================================================================

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Returning CACHED bootstrap config (fetched at {FetchedAt:yyyy-MM-dd HH:mm:ss} UTC) - NOT calling ConfigWebService")]
    private static partial void LogReturningCachedBootstrapConfig(ILogger logger, DateTime fetchedAt);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Bootstrap config fetch attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds:F1}s. Error: {ErrorMessage}")]
    private static partial void LogBootstrapRetry(ILogger logger, int attempt, int maxAttempts, double delaySeconds, string errorMessage);

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

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Returning CACHED configuration '{ConfigName}' (fetched at {FetchedAt:yyyy-MM-dd HH:mm:ss} UTC) - NOT calling ConfigWebService")]
    private static partial void LogReturningCachedConfig(ILogger logger, string configName, DateTime fetchedAt);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Debug, Message = "Configuration '{ConfigName}' cached for {CacheHours:F1} hours")]
    private static partial void LogConfigCached(ILogger logger, string configName, double cacheHours);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Configuration cache invalidated for '{ConfigName}'")]
    private static partial void LogConfigCacheInvalidated(ILogger logger, string configName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Configuration cache cleared ({Count} entries removed)")]
    private static partial void LogConfigCacheCleared(ILogger logger, int count);
}