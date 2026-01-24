using Microsoft.Extensions.Logging;

namespace IFGlobal.Configuration;

public interface IConfigService
{
    /// <summary>
    /// The client ID for JWT audience validation.
    /// For Patient apps, returns the patient portal client (e.g., "dev-login-pps").
    /// For other apps, returns the service client.
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
    string Realm { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<T?> GetConfigAsync<T>(string configName, string accessToken, CancellationToken cancellationToken = default) where T : class;
}