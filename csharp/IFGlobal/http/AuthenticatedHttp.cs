using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace IFGlobal.Http;

/// <summary>
/// Configuration for the authenticated HTTP client.
/// </summary>
public class AuthenticatedHttpOptions
{
    /// <summary>
    /// Function to get the current access token. Returns null if no token is available.
    /// </summary>
    public Func<Task<string?>>? GetAccessToken { get; set; }

    /// <summary>
    /// Optional static token to use when GetAccessToken is not configured.
    /// Useful for service-to-service communication with a pre-fetched token.
    /// </summary>
    public string? StaticToken { get; set; }
}

/// <summary>
/// Static HTTP client that automatically adds authentication headers to requests.
/// Similar to the TypeScript fetchInterceptor pattern.
/// 
/// Usage:
/// 1. Configure once at startup:
///    AuthenticatedHttp.Configure(new AuthenticatedHttpOptions 
///    { 
///        GetAccessToken = () => authService.GetAccessToken() 
///    });
/// 
/// 2. Use anywhere in the application:
///    var result = await AuthenticatedHttp.GetAsync&lt;MyType&gt;("https://api.example.com/data");
///    await AuthenticatedHttp.PostAsync("https://api.example.com/data", myObject);
/// </summary>
public static class AuthenticatedHttp
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static AuthenticatedHttpOptions? _options;
    private static readonly object ConfigLock = new();

    /// <summary>
    /// Configure the authenticated HTTP client with token provider.
    /// Call this once at application startup.
    /// </summary>
    /// <param name="options">Configuration options including token provider.</param>
    public static void Configure(AuthenticatedHttpOptions options)
    {
        lock(ConfigLock)
        {
            _options = options;
        }
    }

    /// <summary>
    /// Configure with a static token (useful for services with pre-fetched tokens).
    /// </summary>
    /// <param name="token">The bearer token to use for all requests.</param>
    public static void ConfigureWithToken(string token)
    {
        Configure(new AuthenticatedHttpOptions { StaticToken = token });
    }

    /// <summary>
    /// Configure with an async token provider function.
    /// </summary>
    /// <param name="getAccessToken">Async function that returns the current access token.</param>
    public static void ConfigureWithTokenProvider(Func<Task<string?>> getAccessToken)
    {
        Configure(new AuthenticatedHttpOptions { GetAccessToken = getAccessToken });
    }

    /// <summary>
    /// Configure with a ServiceTokenManager for automatic token caching and refresh.
    /// </summary>
    /// <param name="tokenManager">The token manager instance.</param>
    public static void ConfigureWithTokenManager(Auth.ServiceTokenManager tokenManager)
    {
        Configure(new AuthenticatedHttpOptions
        {
            GetAccessToken = async () => await tokenManager.GetTokenAsync()
        });
    }

    /// <summary>
    /// Check if the HTTP client has been configured.
    /// </summary>
    public static bool IsConfigured => _options != null;

    // ============================================================================
    // GET
    // ============================================================================

    /// <summary>
    /// Perform a GET request and deserialise the JSON response.
    /// </summary>
    /// <typeparam name="T">The type to deserialise to.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The deserialised response.</returns>
    public static async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await AddAuthHeaderAsync(request);

        var response = await SharedClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    /// <summary>
    /// Perform a GET request and return the raw response.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await AddAuthHeaderAsync(request);

        return await SharedClient.SendAsync(request, cancellationToken);
    }

    // ============================================================================
    // POST
    // ============================================================================

    /// <summary>
    /// Perform a POST request with JSON body and deserialise the response.
    /// </summary>
    /// <typeparam name="TResponse">The response type to deserialise to.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body (will be serialised to JSON).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The deserialised response.</returns>
    public static async Task<TResponse?> PostAsync<TResponse>(string url, object? body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeaderAsync(request);

        if(body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await SharedClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
    }

    /// <summary>
    /// Perform a POST request with JSON body and return the raw response.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body (will be serialised to JSON).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> PostAsync(string url, object? body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        await AddAuthHeaderAsync(request);

        if(body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await SharedClient.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Perform a fire-and-forget POST request. Errors are silently ignored.
    /// Useful for logging and telemetry where failures should not impact the application.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body (will be serialised to JSON).</param>
    public static void PostFireAndForget(string url, object? body)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                await AddAuthHeaderAsync(request);

                if(body != null)
                {
                    var json = JsonSerializer.Serialize(body, JsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                await SharedClient.SendAsync(request);
            }
            catch
            {
                // Silently ignore errors for fire-and-forget requests
            }
        });
    }

    // ============================================================================
    // PUT
    // ============================================================================

    /// <summary>
    /// Perform a PUT request with JSON body and deserialise the response.
    /// </summary>
    /// <typeparam name="TResponse">The response type to deserialise to.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body (will be serialised to JSON).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The deserialised response.</returns>
    public static async Task<TResponse?> PutAsync<TResponse>(string url, object? body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        await AddAuthHeaderAsync(request);

        if(body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await SharedClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TResponse>(content, JsonOptions);
    }

    /// <summary>
    /// Perform a PUT request with JSON body and return the raw response.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="body">The request body (will be serialised to JSON).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> PutAsync(string url, object? body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        await AddAuthHeaderAsync(request);

        if(body != null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return await SharedClient.SendAsync(request, cancellationToken);
    }

    // ============================================================================
    // DELETE
    // ============================================================================

    /// <summary>
    /// Perform a DELETE request and deserialise the response.
    /// </summary>
    /// <typeparam name="T">The response type to deserialise to.</typeparam>
    /// <param name="url">The request URL.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The deserialised response.</returns>
    public static async Task<T?> DeleteAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await AddAuthHeaderAsync(request);

        var response = await SharedClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    /// <summary>
    /// Perform a DELETE request and return the raw response.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    public static async Task<HttpResponseMessage> DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await AddAuthHeaderAsync(request);

        return await SharedClient.SendAsync(request, cancellationToken);
    }

    // ============================================================================
    // HELPERS
    // ============================================================================

    /// <summary>
    /// Add the Authorization header to the request if a token is available.
    /// </summary>
    private static async Task AddAuthHeaderAsync(HttpRequestMessage request)
    {
        string? token = null;

        if(_options != null)
        {
            // Try async token provider first
            if(_options.GetAccessToken != null)
            {
                try
                {
                    token = await _options.GetAccessToken();
                }
                catch
                {
                    // Token provider failed, fall back to static token
                }
            }

            // Fall back to static token
            token ??= _options.StaticToken;
        }

        if(!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Reset configuration (primarily for testing).
    /// </summary>
    public static void Reset()
    {
        lock(ConfigLock)
        {
            _options = null;
        }
    }
}