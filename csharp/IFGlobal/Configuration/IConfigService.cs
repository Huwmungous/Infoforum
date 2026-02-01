using Microsoft.Extensions.Logging;

namespace IFGlobal.Configuration;

public interface IConfigService
{
    /// <summary>
    /// The client ID for JWT audience validation.
    /// </summary>
    string ClientId { get; }

    /// <summary>
    /// The service client ID for service-to-service authentication.
    /// Always returns the service client (e.g., "dev-login-svc").
    /// </summary>
    string ServiceClientId { get; }

    /// <summary>
    /// The client secret for service authentication.
    /// Only available for service type bootstrap requests.
    /// </summary>
    string? ClientSecret { get; }

    string OpenIdConfig { get; }
    string? LoggerService { get; }
    LogLevel LogLevel { get; }

    /// <summary>
    /// The Keycloak realm name (e.g., "LongmanRd"). Retrieved from bootstrap config.
    /// </summary>
    string Realm { get; }

    /// <summary>
    /// The application domain (e.g., "Infoforum", "BreakTackle"). From appsettings.
    /// </summary>
    string AppDomain { get; }

    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches configuration with caching. Subsequent calls return cached values until they expire.
    /// </summary>
    /// <typeparam name="T">The type to deserialise the configuration into</typeparam>
    /// <param name="configName">The configuration key (e.g., "firebirddb", "loggerdb")</param>
    /// <param name="accessToken">Bearer token for authentication</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <param name="cacheDuration">Optional cache duration (defaults to 24 hours)</param>
    /// <returns>The deserialised configuration object, or null if not found</returns>
    Task<T?> GetConfigAsync<T>(
        string configName,
        string accessToken,
        CancellationToken cancellationToken = default,
        TimeSpan? cacheDuration = null) where T : class;

    /// <summary>
    /// Invalidates a specific cached configuration, forcing the next request to fetch fresh data.
    /// </summary>
    /// <param name="configName">The configuration key to invalidate</param>
    /// <returns>True if the cache entry was found and removed, false otherwise</returns>
    Task<bool> InvalidateCacheAsync(string configName);

    /// <summary>
    /// Clears all cached configuration values.
    /// </summary>
    Task ClearCacheAsync();

    /// <summary>
    /// Gets information about the current cache state.
    /// </summary>
    IReadOnlyDictionary<string, (DateTime FetchedAt, DateTime ExpiresAt, bool IsExpired)> GetCacheInfo();
}