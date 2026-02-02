using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IFGlobal.Auth;

/// <summary>
/// Handles user authentication using OAuth PKCE flow for desktop applications.
/// Designed to work with an embedded WebView rather than launching a browser.
/// 
/// Usage:
///   var auth = new UserAuthenticator("infoforum-user");
///   auth.SetAuthority("https://keycloak.example.com/realms/myrealm");
///   
///   // Get the login URL to display in WebView
///   var (loginUrl, expectedState) = auth.GetLoginUrl();
///   
///   // After WebView navigates to callback URL:
///   var success = await auth.HandleCallbackAsync(callbackUrl);
/// </summary>
public sealed class UserAuthenticator : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly int _callbackPort;
    
    private string? _authority;
    private string? _codeVerifier;
    private string? _expectedState;

    /// <summary>
    /// Current access token (null if not authenticated).
    /// </summary>
    public string? AccessToken { get; private set; }
    
    /// <summary>
    /// Current refresh token for obtaining new access tokens.
    /// </summary>
    public string? RefreshToken { get; private set; }
    
    /// <summary>
    /// When the current access token expires (UTC).
    /// </summary>
    public DateTime TokenExpiry { get; private set; }
    
    /// <summary>
    /// The authenticated user's username.
    /// </summary>
    public string? Username { get; private set; }
    
    /// <summary>
    /// The authenticated user's unique ID (Keycloak subject).
    /// </summary>
    public string? UserId { get; private set; }
    
    /// <summary>
    /// Whether the user is currently authenticated with a valid token.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && DateTime.UtcNow < TokenExpiry;

    /// <summary>
    /// The redirect URI used for OAuth callbacks.
    /// </summary>
    public string RedirectUri => $"http://localhost:{_callbackPort}/callback";

    /// <summary>
    /// Raised when authentication state changes.
    /// </summary>
    public event Action<bool>? AuthenticationChanged;

    /// <summary>
    /// Creates a new UserAuthenticator.
    /// </summary>
    /// <param name="clientId">The OAuth client ID (e.g., "infoforum-user").</param>
    /// <param name="callbackPort">Port for OAuth callback (default 7890).</param>
    public UserAuthenticator(string clientId = "infoforum-user", int callbackPort = 7890)
    {
        _httpClient = new HttpClient();
        _clientId = clientId;
        _callbackPort = callbackPort;
    }

    /// <summary>
    /// Sets the Keycloak authority URL (realm URL).
    /// Must be called before authentication.
    /// </summary>
    /// <param name="authority">Full realm URL, e.g., "https://keycloak.example.com/realms/myrealm".</param>
    public void SetAuthority(string authority)
    {
        _authority = authority.TrimEnd('/');
    }

    /// <summary>
    /// Gets the login URL to display in a WebView.
    /// Call this to initiate the login flow.
    /// </summary>
    /// <returns>A tuple containing the login URL and the expected state value for validation.</returns>
    public (string LoginUrl, string ExpectedState) GetLoginUrl()
    {
        if (string.IsNullOrEmpty(_authority))
        {
            throw new InvalidOperationException("Authority not set. Call SetAuthority first.");
        }

        // Generate PKCE values
        _codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(_codeVerifier);
        _expectedState = Guid.NewGuid().ToString("N");

        var loginUrl = $"{_authority}/protocol/openid-connect/auth" +
            $"?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&response_type=code" +
            $"&scope=openid%20profile%20email" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&state={_expectedState}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        return (loginUrl, _expectedState);
    }

    /// <summary>
    /// Checks if a URL is the OAuth callback URL.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <returns>True if this is the callback URL.</returns>
    public bool IsCallbackUrl(string url)
    {
        return url.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Handles the OAuth callback URL after successful login.
    /// Call this when the WebView navigates to the callback URL.
    /// </summary>
    /// <param name="callbackUrl">The full callback URL including query parameters.</param>
    /// <returns>True if authentication succeeded.</returns>
    public async Task<bool> HandleCallbackAsync(string callbackUrl)
    {
        if (string.IsNullOrEmpty(_codeVerifier) || string.IsNullOrEmpty(_expectedState))
        {
            throw new InvalidOperationException("Login flow not initiated. Call GetLoginUrl first.");
        }

        try
        {
            // Parse the callback URL manually
            var uri = new Uri(callbackUrl);
            var queryParams = ParseQueryString(uri.Query);
            
            var code = queryParams.GetValueOrDefault("code");
            var state = queryParams.GetValueOrDefault("state");
            var error = queryParams.GetValueOrDefault("error");

            // Check for errors
            if (!string.IsNullOrEmpty(error))
            {
                var errorDescription = queryParams.GetValueOrDefault("error_description") ?? error;
                throw new InvalidOperationException($"Authentication failed: {errorDescription}");
            }

            // Validate state
            if (state != _expectedState)
            {
                throw new InvalidOperationException("State mismatch - possible CSRF attack");
            }

            if (string.IsNullOrEmpty(code))
            {
                throw new InvalidOperationException("No authorisation code received");
            }

            // Exchange code for tokens
            await ExchangeCodeForTokensAsync(code, _codeVerifier);

            AuthenticationChanged?.Invoke(true);
            return true;
        }
        catch (Exception)
        {
            AuthenticationChanged?.Invoke(false);
            throw;
        }
        finally
        {
            // Clear PKCE values
            _codeVerifier = null;
            _expectedState = null;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrEmpty(query))
            return result;

        // Remove leading '?' if present
        if (query.StartsWith('?'))
            query = query[1..];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = Uri.UnescapeDataString(parts[1]);
                result[key] = value;
            }
            else if (parts.Length == 1)
            {
                result[Uri.UnescapeDataString(parts[0])] = string.Empty;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// </summary>
    /// <returns>The access token, or null if not authenticated.</returns>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(AccessToken))
            return null;

        // Refresh if expiring soon (within 30 seconds)
        if (DateTime.UtcNow.AddSeconds(30) >= TokenExpiry && !string.IsNullOrEmpty(RefreshToken))
        {
            await RefreshTokensAsync();
        }

        return AccessToken;
    }

    /// <summary>
    /// Attempts to refresh the current session using the refresh token.
    /// Can be called on app startup to restore a previous session.
    /// </summary>
    /// <param name="refreshToken">The refresh token from a previous session.</param>
    /// <returns>True if the refresh succeeded.</returns>
    public async Task<bool> TryRefreshSessionAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(_authority))
        {
            throw new InvalidOperationException("Authority not set. Call SetAuthority first.");
        }

        RefreshToken = refreshToken;
        
        try
        {
            await RefreshTokensAsync();
            AuthenticationChanged?.Invoke(true);
            return true;
        }
        catch
        {
            ClearTokens();
            return false;
        }
    }

    /// <summary>
    /// Clears all authentication state (logs out locally).
    /// </summary>
    public void Logout()
    {
        ClearTokens();
        AuthenticationChanged?.Invoke(false);
    }

    private void ClearTokens()
    {
        AccessToken = null;
        RefreshToken = null;
        TokenExpiry = DateTime.MinValue;
        Username = null;
        UserId = null;
    }

    private async Task ExchangeCodeForTokensAsync(string code, string codeVerifier)
    {
        var tokenEndpoint = $"{_authority}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _clientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = codeVerifier
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        ParseTokenResponse(json);
    }

    private async Task RefreshTokensAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(_authority))
            throw new InvalidOperationException("Cannot refresh: no refresh token or authority");

        var tokenEndpoint = $"{_authority}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _clientId,
            ["refresh_token"] = RefreshToken
        });

        var response = await _httpClient.PostAsync(tokenEndpoint, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        ParseTokenResponse(json);
    }

    private void ParseTokenResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        AccessToken = root.GetProperty("access_token").GetString();
        RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 300;
        TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

        // Parse JWT to get user info
        if (!string.IsNullOrEmpty(AccessToken))
        {
            ParseJwtClaims(AccessToken);
        }
    }

    private void ParseJwtClaims(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return;

            var payload = parts[1];
            // Add padding if needed
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
            var jsonString = Encoding.UTF8.GetString(jsonBytes);

            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            Username = root.TryGetProperty("preferred_username", out var un) ? un.GetString() : null;
            UserId = root.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
        }
        catch
        {
            // Ignore JWT parsing errors - claims are nice-to-have
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
