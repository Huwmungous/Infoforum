using System.Windows;
using IFGlobal.Auth;
using Microsoft.Web.WebView2.Core;

namespace ChitterChatterClient.Views;

/// <summary>
/// Login window that embeds Keycloak login using WebView2.
/// Shows as a modal dialog and returns when authentication completes.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly UserAuthenticator _authenticator;
    private string? _loginUrl;
    private bool _isInitialised;

    /// <summary>
    /// Whether authentication was successful.
    /// </summary>
    public bool AuthenticationSucceeded { get; private set; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Creates a new LoginWindow.
    /// </summary>
    /// <param name="authenticator">The authenticator to use for the OAuth flow.</param>
    public LoginWindow(UserAuthenticator authenticator)
    {
        InitializeComponent();
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        
        Loaded += LoginWindow_Loaded;
    }

    private async void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await InitialiseWebViewAsync();
    }

    private async Task InitialiseWebViewAsync()
    {
        try
        {
            ShowLoading("Initialising...");

            // Initialise WebView2
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ChitterChatter", "WebView2");
            
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await LoginWebView.EnsureCoreWebView2Async(env);

            // Configure WebView2
            LoginWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            LoginWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            LoginWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            
            // Clear any existing cookies/cache for a clean login
            await LoginWebView.CoreWebView2.Profile.ClearBrowsingDataAsync();

            _isInitialised = true;

            // Navigate to login page
            await NavigateToLoginAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to initialise login: {ex.Message}");
        }
    }

    private async Task NavigateToLoginAsync()
    {
        try
        {
            ShowLoading("Loading login page...");

            // Get the login URL from the authenticator
            var (loginUrl, _) = _authenticator.GetLoginUrl();
            _loginUrl = loginUrl;

            // Navigate to it
            LoginWebView.Source = new Uri(loginUrl);
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load login: {ex.Message}");
        }
    }

    private async void LoginWebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        StatusText.Text = "Loading...";

        // Check if this is the OAuth callback
        if (_authenticator.IsCallbackUrl(e.Uri))
        {
            // Cancel the navigation - we'll handle it ourselves
            e.Cancel = true;

            ShowLoading("Completing sign in...");

            try
            {
                // Handle the callback
                var success = await _authenticator.HandleCallbackAsync(e.Uri);
                
                if (success)
                {
                    AuthenticationSucceeded = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError("Authentication failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Authentication failed: {ex.Message}");
            }
        }
        else
        {
            // Show the WebView for normal navigation
            HideOverlays();
        }
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInitialised)
        {
            _ = NavigateToLoginAsync();
        }
        else
        {
            _ = InitialiseWebViewAsync();
        }
    }

    private void ShowLoading(string message)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        StatusText.Text = message;
    }

    private void ShowError(string message)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Visible;
        ErrorText.Text = message;
        ErrorMessage = message;
        StatusText.Text = "Error occurred";
    }

    private void HideOverlays()
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Collapsed;
        StatusText.Text = "Please sign in with your credentials";
    }
}
