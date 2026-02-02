using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using ChitterChatterClient.Views;
using IFGlobal.Auth;

namespace ChitterChatterClient.Services;

/// <summary>
/// Handles user authentication using embedded Keycloak login.
/// Wraps UserAuthenticator and manages the login window.
/// </summary>
public sealed class AuthService : IDisposable
{
    private readonly UserAuthenticator _authenticator;
    private readonly string _tokenStorePath;

    public string? AccessToken => _authenticator.AccessToken;
    public string? RefreshToken => _authenticator.RefreshToken;
    public DateTime TokenExpiry => _authenticator.TokenExpiry;
    public string? Username => _authenticator.Username;
    public string? UserId => _authenticator.UserId;
    public bool IsAuthenticated => _authenticator.IsAuthenticated;

    public event Action<bool>? AuthenticationChanged;

    public AuthService(string clientId = "infoforum-user", int callbackPort = 7890)
    {
        _authenticator = new UserAuthenticator(clientId, callbackPort);
        _authenticator.AuthenticationChanged += OnAuthenticationChanged;

        // Store tokens in local app data
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChitterChatter");
        Directory.CreateDirectory(appDataPath);
        _tokenStorePath = Path.Combine(appDataPath, "auth.json");
    }

    /// <summary>
    /// Sets the authority URL after fetching from ConfigService.
    /// </summary>
    public void SetAuthority(string authority)
    {
        _authenticator.SetAuthority(authority);
    }

    /// <summary>
    /// Attempts to restore a previous session using stored refresh token.
    /// Call this on app startup before showing the login UI.
    /// </summary>
    /// <returns>True if session was restored successfully.</returns>
    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var storedTokens = await LoadStoredTokensAsync();
            if (storedTokens?.RefreshToken != null)
            {
                var success = await _authenticator.TryRefreshSessionAsync(storedTokens.RefreshToken);
                if (success)
                {
                    // Save the new tokens
                    await SaveTokensAsync();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restore session: {ex.Message}");
        }

        // Clear any invalid stored tokens
        await ClearStoredTokensAsync();
        return false;
    }

    /// <summary>
    /// Shows the embedded login window and waits for authentication.
    /// </summary>
    /// <returns>True if login succeeded.</returns>
    public async Task<bool> LoginAsync()
    {
        try
        {
            // Must show dialog on UI thread
            var result = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var loginWindow = new LoginWindow(_authenticator);
                
                // Only set owner if MainWindow is already shown
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null && mainWindow.IsLoaded)
                {
                    loginWindow.Owner = mainWindow;
                }
                else
                {
                    // Show in centre of screen if no owner
                    loginWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                return loginWindow.ShowDialog();
            });

            if (result == true && _authenticator.IsAuthenticated)
            {
                // Save tokens for session restoration
                await SaveTokensAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Login failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Logs out by clearing tokens.
    /// </summary>
    public async Task LogoutAsync()
    {
        _authenticator.Logout();
        await ClearStoredTokensAsync();
    }

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        var token = await _authenticator.GetAccessTokenAsync();
        
        // If we refreshed, save the new tokens
        if (token != null && _authenticator.RefreshToken != null)
        {
            await SaveTokensAsync();
        }

        return token;
    }

    private void OnAuthenticationChanged(bool isAuthenticated)
    {
        AuthenticationChanged?.Invoke(isAuthenticated);
    }

    private async Task SaveTokensAsync()
    {
        try
        {
            var tokens = new StoredTokens
            {
                RefreshToken = _authenticator.RefreshToken
            };

            var json = JsonSerializer.Serialize(tokens);
            await File.WriteAllTextAsync(_tokenStorePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save tokens: {ex.Message}");
        }
    }

    private async Task<StoredTokens?> LoadStoredTokensAsync()
    {
        try
        {
            if (File.Exists(_tokenStorePath))
            {
                var json = await File.ReadAllTextAsync(_tokenStorePath);
                return JsonSerializer.Deserialize<StoredTokens>(json);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load tokens: {ex.Message}");
        }

        return null;
    }

    private async Task ClearStoredTokensAsync()
    {
        try
        {
            if (File.Exists(_tokenStorePath))
            {
                await Task.Run(() => File.Delete(_tokenStorePath));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to clear tokens: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _authenticator.Dispose();
    }

    private sealed class StoredTokens
    {
        public string? RefreshToken { get; set; }
    }
}
