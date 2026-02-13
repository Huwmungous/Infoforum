using IFGlobal.Configuration;
using System.Text.Json;

namespace IFGlobal.Auth;

/// <summary>
/// Manages service account tokens with automatic caching and refresh.
/// Thread-safe for use in long-running services.
/// 
/// Usage:
///   var tokenManager = new ServiceTokenManager(configService);
///   var token = await tokenManager.GetTokenAsync(); // Cached, auto-refreshes
/// </summary>
public class ServiceTokenManager : IDisposable
{
    private readonly IConfigService _configService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly string? _openIdConfig;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly int _refreshBufferSeconds;

    // FIX 1: Use a single HttpClient instead of creating one per refresh.
    // Creating/disposing HttpClient per call causes socket exhaustion under load.
    // If an IHttpClientFactory is available (preferred), we use that instead.
    private readonly HttpClient? _ownedHttpClient;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private bool _disposed;

    /// <summary>
    /// Create a token manager using ConfigService for credentials.
    /// </summary>
    /// <param name="configService">Initialised ConfigService with service credentials.</param>
    /// <param name="httpClientFactory">Optional HTTP client factory (preferred for connection pooling).</param>
    /// <param name="refreshBufferSeconds">Seconds before expiry to trigger refresh (default 60).</param>
    public ServiceTokenManager(
        IConfigService configService,
        IHttpClientFactory? httpClientFactory = null,
        int refreshBufferSeconds = 60)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _httpClientFactory = httpClientFactory;
        _refreshBufferSeconds = refreshBufferSeconds;

        if(!configService.IsInitialized)
        {
            throw new InvalidOperationException("ConfigService must be initialized first");
        }

        if(string.IsNullOrEmpty(configService.ClientSecret))
        {
            throw new InvalidOperationException(
                "ClientSecret not available from ConfigService. " +
                "Ensure bootstrap was requested with type=service.");
        }

        // Only create our own HttpClient if no factory was provided
        if(_httpClientFactory == null)
        {
            _ownedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }
    }

    /// <summary>
    /// Create a token manager with explicit credentials.
    /// </summary>
    /// <param name="openIdConfig">OpenID Connect configuration URL (realm URL).</param>
    /// <param name="clientId">Service client ID.</param>
    /// <param name="clientSecret">Service client secret.</param>
    /// <param name="httpClientFactory">Optional HTTP client factory (preferred for connection pooling).</param>
    /// <param name="refreshBufferSeconds">Seconds before expiry to trigger refresh (default 60).</param>
    public ServiceTokenManager(
        string openIdConfig,
        string clientId,
        string clientSecret,
        IHttpClientFactory? httpClientFactory = null,
        int refreshBufferSeconds = 60)
    {
        _openIdConfig = openIdConfig ?? throw new ArgumentNullException(nameof(openIdConfig));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        _httpClientFactory = httpClientFactory;
        _refreshBufferSeconds = refreshBufferSeconds;
        _configService = null!;

        if(_httpClientFactory == null)
        {
            _ownedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }
    }

    /// <summary>
    /// Get a valid access token. Returns cached token if still valid,
    /// otherwise fetches a new one.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Fast path: return cached token if still valid
        if(!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }

        // Need to refresh - acquire lock to prevent multiple concurrent refreshes
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock (another thread may have refreshed)
            if(!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _cachedToken;
            }

            // Fetch new token
            await RefreshTokenAsync(cancellationToken);
            return _cachedToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Force a token refresh, even if the current token is still valid.
    /// </summary>
    public async Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            await RefreshTokenAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Check if the current token is valid (not expired).
    /// </summary>
    public bool IsTokenValid => !string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry;

    /// <summary>
    /// Get the token expiry time (UTC).
    /// </summary>
    public DateTime TokenExpiry => _tokenExpiry;

    /// <summary>
    /// Get time until token expires.
    /// </summary>
    public TimeSpan TimeUntilExpiry => _tokenExpiry > DateTime.UtcNow
        ? _tokenExpiry - DateTime.UtcNow
        : TimeSpan.Zero;

    private HttpClient GetHttpClient()
    {
        if(_httpClientFactory != null)
            return _httpClientFactory.CreateClient();

        return _ownedHttpClient
            ?? throw new ObjectDisposedException(nameof(ServiceTokenManager));
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var openIdConfig = _openIdConfig ?? _configService.OpenIdConfig;
        var clientId = _clientId ?? _configService.ServiceClientId;
        var clientSecret = _clientSecret ?? _configService.ClientSecret;

        if(string.IsNullOrEmpty(openIdConfig))
            throw new InvalidOperationException("OpenIdConfig is not configured");
        if(string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException("ClientId is not configured");
        if(string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("ClientSecret is not configured");

        // FIX 1 continued: reuse the HttpClient rather than creating a new one each time.
        var httpClient = GetHttpClient();
        var tokenEndpoint = $"{openIdConfig}/protocol/openid-connect/token";

        var formData = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);

        if(!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Failed to authenticate service: {(int)response.StatusCode} - {error}");
        }

        var tokenResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

        _cachedToken = tokenData.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("No access token in response");

        // Calculate expiry with buffer
        var expiresIn = tokenData.TryGetProperty("expires_in", out var exp)
            ? exp.GetInt32()
            : 300; // Default to 5 minutes if not specified

        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - _refreshBufferSeconds);
    }

    /// <summary>
    /// Start background auto-refresh. Token will be refreshed automatically
    /// before it expires. Returns a CancellationTokenSource to stop the refresh loop.
    /// </summary>
    /// <param name="onRefreshError">Optional callback for refresh errors.</param>
    public CancellationTokenSource StartAutoRefresh(Action<Exception>? onRefreshError = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            // FIX 2: Track consecutive failures for exponential backoff with jitter.
            // The original used a fixed 30s retry which can cause thundering herd
            // if multiple services fail simultaneously.
            int consecutiveFailures = 0;
            const int maxBackoffSeconds = 300; // Cap at 5 minutes

            while(!cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Wait until close to expiry
                    var delay = TimeUntilExpiry - TimeSpan.FromSeconds(30);
                    if(delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cts.Token);
                    }

                    // Refresh the token
                    await GetTokenAsync(cts.Token);
                    consecutiveFailures = 0; // Reset on success
                }
                catch(OperationCanceledException)
                {
                    break;
                }
                catch(Exception ex)
                {
                    consecutiveFailures++;
                    onRefreshError?.Invoke(ex);

                    // Exponential backoff with jitter to avoid thundering herd
                    var baseDelay = Math.Min(
                        30 * Math.Pow(2, consecutiveFailures - 1),
                        maxBackoffSeconds);
                    var jitter = Random.Shared.NextDouble() * baseDelay * 0.3;
                    var retryDelay = TimeSpan.FromSeconds(baseDelay + jitter);

                    try
                    {
                        await Task.Delay(retryDelay, cts.Token);
                    }
                    catch(OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, cts.Token);

        return cts;
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed = true;

        _ownedHttpClient?.Dispose();
        _refreshLock.Dispose();
    }
}
