using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IFGlobal.Http;

namespace LogSender;

public class MainForm : Form
{
    // Connection controls
    private GroupBox grpConnection = null!;
    private Label lblLoggerUrl = null!;
    private TextBox txtLoggerUrl = null!;
    private Label lblAuthority = null!;
    private TextBox txtAuthority = null!;
    private Label lblRealm = null!;
    private TextBox txtRealm = null!;
    private Label lblClientId = null!;
    private TextBox txtClientId = null!;
    private Label lblRedirectPort = null!;
    private NumericUpDown nudRedirectPort = null!;
    private Button btnAuthenticate = null!;
    private Label lblAuthStatus = null!;

    // Log entry controls
    private GroupBox grpLogEntry = null!;
    private Label lblApplication = null!;
    private TextBox txtApplication = null!;
    private Label lblCategory = null!;
    private TextBox txtCategory = null!;
    private Label lblLevel = null!;
    private ComboBox cboLevel = null!;
    private Label lblEnvironment = null!;
    private TextBox txtEnvironment = null!;
    private Label lblMessage = null!;
    private TextBox txtMessage = null!;
    private Button btnSendLog = null!;
    private Button btnSendMultiple = null!;
    private NumericUpDown nudCount = null!;
    private Label lblCount = null!;

    // Status
    private TextBox txtStatus = null!;

    // State
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private HttpListener? _callbackListener;

    public MainForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // ============================================
        // Connection Group
        // ============================================
        grpConnection = new GroupBox
        {
            Text = "Connection",
            Location = new Point(12, 12),
            Size = new Size(560, 150)
        };

        lblLoggerUrl = new Label { Text = "Logger URL:", Location = new Point(10, 25), Size = new Size(75, 20) };
        txtLoggerUrl = new TextBox
        {
            Location = new Point(90, 22),
            Size = new Size(455, 23),
            Text = "https://longmanrd.net/logger"
        };

        lblAuthority = new Label { Text = "Authority:", Location = new Point(10, 52), Size = new Size(75, 20) };
        txtAuthority = new TextBox
        {
            Location = new Point(90, 49),
            Size = new Size(455, 23),
            Text = "https://longmanrd.net/auth/realms/LongmanRd"
        };

        lblRealm = new Label { Text = "Realm:", Location = new Point(10, 79), Size = new Size(50, 20) };
        txtRealm = new TextBox
        {
            Location = new Point(65, 76),
            Size = new Size(120, 23),
            Text = "LongmanRd"
        };

        lblClientId = new Label { Text = "Client ID:", Location = new Point(195, 79), Size = new Size(60, 20) };
        txtClientId = new TextBox
        {
            Location = new Point(260, 76),
            Size = new Size(140, 23),
            Text = "infoforum-user"
        };

        lblRedirectPort = new Label { Text = "Port:", Location = new Point(410, 79), Size = new Size(35, 20) };
        nudRedirectPort = new NumericUpDown
        {
            Location = new Point(450, 76),
            Size = new Size(70, 23),
            Minimum = 1024,
            Maximum = 65535,
            Value = 5050
        };

        btnAuthenticate = new Button
        {
            Text = "Login (Browser)",
            Location = new Point(90, 110),
            Size = new Size(120, 28)
        };
        btnAuthenticate.Click += BtnAuthenticate_Click;

        lblAuthStatus = new Label
        {
            Text = "Not authenticated",
            Location = new Point(220, 115),
            Size = new Size(325, 20),
            ForeColor = Color.Gray
        };

        grpConnection.Controls.AddRange(new Control[]
        {
            lblLoggerUrl, txtLoggerUrl,
            lblAuthority, txtAuthority,
            lblRealm, txtRealm,
            lblClientId, txtClientId,
            lblRedirectPort, nudRedirectPort,
            btnAuthenticate, lblAuthStatus
        });

        // ============================================
        // Log Entry Group
        // ============================================
        grpLogEntry = new GroupBox
        {
            Text = "Log Entry",
            Location = new Point(12, 170),
            Size = new Size(560, 195)
        };

        lblApplication = new Label { Text = "Application:", Location = new Point(10, 25), Size = new Size(75, 20) };
        txtApplication = new TextBox
        {
            Location = new Point(90, 22),
            Size = new Size(180, 23),
            Text = "LogSender"
        };

        lblCategory = new Label { Text = "Category:", Location = new Point(280, 25), Size = new Size(60, 20) };
        txtCategory = new TextBox
        {
            Location = new Point(345, 22),
            Size = new Size(200, 23),
            Text = "TestController"
        };

        lblLevel = new Label { Text = "Level:", Location = new Point(10, 52), Size = new Size(75, 20) };
        cboLevel = new ComboBox
        {
            Location = new Point(90, 49),
            Size = new Size(120, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cboLevel.Items.AddRange(new object[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" });
        cboLevel.SelectedIndex = 2; // Information

        lblEnvironment = new Label { Text = "Environment:", Location = new Point(220, 52), Size = new Size(80, 20) };
        txtEnvironment = new TextBox
        {
            Location = new Point(305, 49),
            Size = new Size(80, 23),
            Text = "DEV"
        };

        lblMessage = new Label { Text = "Message:", Location = new Point(10, 79), Size = new Size(75, 20) };
        txtMessage = new TextBox
        {
            Location = new Point(90, 76),
            Size = new Size(455, 60),
            Multiline = true,
            Text = "Test log message from LogSender"
        };

        btnSendLog = new Button
        {
            Text = "Send Log",
            Location = new Point(90, 145),
            Size = new Size(100, 30)
        };
        btnSendLog.Click += BtnSendLog_Click;

        lblCount = new Label { Text = "Count:", Location = new Point(210, 152), Size = new Size(45, 20) };
        nudCount = new NumericUpDown
        {
            Location = new Point(260, 149),
            Size = new Size(60, 23),
            Minimum = 1,
            Maximum = 100,
            Value = 5
        };

        btnSendMultiple = new Button
        {
            Text = "Send Multiple",
            Location = new Point(330, 145),
            Size = new Size(100, 30)
        };
        btnSendMultiple.Click += BtnSendMultiple_Click;

        grpLogEntry.Controls.AddRange(new Control[]
        {
            lblApplication, txtApplication,
            lblCategory, txtCategory,
            lblLevel, cboLevel,
            lblEnvironment, txtEnvironment,
            lblMessage, txtMessage,
            btnSendLog, lblCount, nudCount, btnSendMultiple
        });

        // ============================================
        // Status TextBox
        // ============================================
        txtStatus = new TextBox
        {
            Location = new Point(12, 375),
            Size = new Size(560, 135),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9F)
        };

        // ============================================
        // Form
        // ============================================
        this.ClientSize = new Size(584, 521);
        this.Controls.AddRange(new Control[] { grpConnection, grpLogEntry, txtStatus });
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "Log Sender";
        this.FormClosing += MainForm_FormClosing;

        this.ResumeLayout(false);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _callbackListener?.Close();
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        txtStatus.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        txtStatus.SelectionStart = txtStatus.TextLength;
        txtStatus.ScrollToCaret();
    }

    private void UpdateAuthStatus(string message, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateAuthStatus(message, color));
            return;
        }
        lblAuthStatus.Text = message;
        lblAuthStatus.ForeColor = color;
    }

    private async void BtnAuthenticate_Click(object? sender, EventArgs e)
    {
        try
        {
            btnAuthenticate.Enabled = false;
            UpdateAuthStatus("Starting login...", Color.Orange);

            var authority = txtAuthority.Text.Trim();
            var clientId = txtClientId.Text.Trim();
            var port = (int)nudRedirectPort.Value;
            var redirectUri = $"http://localhost:{port}/callback";

            // Generate PKCE code verifier and challenge
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Build authorization URL
            var state = Guid.NewGuid().ToString("N");
            var authUrl = $"{authority}/protocol/openid-connect/auth?" +
                $"client_id={Uri.EscapeDataString(clientId)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                $"response_type=code&" +
                $"scope=openid&" +
                $"state={state}&" +
                $"code_challenge={codeChallenge}&" +
                $"code_challenge_method=S256";

            Log($"Starting PKCE login flow...");
            Log($"Redirect URI: {redirectUri}");

            // Start local HTTP listener for callback
            _callbackListener?.Close();
            _callbackListener = new HttpListener();
            _callbackListener.Prefixes.Add($"http://localhost:{port}/");
            
            try
            {
                _callbackListener.Start();
            }
            catch (HttpListenerException ex)
            {
                Log($"Failed to start listener on port {port}: {ex.Message}");
                Log("Try a different port or run as administrator.");
                UpdateAuthStatus("Failed to start listener", Color.Red);
                return;
            }

            // Open browser for login
            Log("Opening browser for login...");
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            UpdateAuthStatus("Waiting for browser login...", Color.Orange);

            // Wait for callback
            var context = await _callbackListener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            // Parse callback
            var query = request.Url?.Query?.TrimStart('?') ?? "";
            var queryParams = ParseQueryString(query);
            queryParams.TryGetValue("code", out var code);
            queryParams.TryGetValue("state", out var returnedState);
            queryParams.TryGetValue("error", out var error);

            // Send response to browser
            var responseHtml = error == null
                ? "<html><body><h2>Login successful!</h2><p>You can close this window and return to LogSender.</p></body></html>"
                : $"<html><body><h2>Login failed</h2><p>Error: {error}</p></body></html>";
            
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html";
            await response.OutputStream.WriteAsync(buffer);
            response.Close();

            _callbackListener.Close();
            _callbackListener = null;

            if (!string.IsNullOrEmpty(error))
            {
                Log($"Login failed: {error}");
                UpdateAuthStatus($"Login failed: {error}", Color.Red);
                return;
            }

            if (returnedState != state)
            {
                Log("State mismatch - possible CSRF attack");
                UpdateAuthStatus("Security error: state mismatch", Color.Red);
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                Log("No authorization code received");
                UpdateAuthStatus("No authorization code", Color.Red);
                return;
            }

            Log("Authorization code received, exchanging for tokens...");

            // Exchange code for tokens
            var tokenUrl = $"{authority}/protocol/openid-connect/token";
            using var httpClient = new HttpClient();
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier
            });

            var tokenResponse = await httpClient.PostAsync(tokenUrl, tokenRequest);
            var tokenBody = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                Log($"Token exchange failed: {tokenResponse.StatusCode}");
                Log(tokenBody);
                UpdateAuthStatus("Token exchange failed", Color.Red);
                return;
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenBody);
            _accessToken = tokenData.GetProperty("access_token").GetString();
            var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 30);

            // Configure AuthenticatedHttp
            AuthenticatedHttp.ConfigureWithToken(_accessToken!);

            // Try to get username from token
            var username = GetUsernameFromToken(_accessToken!);

            Log($"Login successful! Token expires in {expiresIn}s");
            UpdateAuthStatus($"Logged in as {username}. Expires: {_tokenExpiry.ToLocalTime():HH:mm:ss}", Color.Green);
        }
        catch (Exception ex)
        {
            Log($"Auth error: {ex.Message}");
            UpdateAuthStatus($"Error: {ex.Message}", Color.Red);
            _accessToken = null;
        }
        finally
        {
            btnAuthenticate.Enabled = true;
        }
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GetUsernameFromToken(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return "unknown";

            var payload = parts[1];
            // Add padding if needed
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }
            payload = payload.Replace('-', '+').Replace('_', '/');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var claims = JsonSerializer.Deserialize<JsonElement>(json);

            if (claims.TryGetProperty("preferred_username", out var username))
                return username.GetString() ?? "unknown";
            if (claims.TryGetProperty("sub", out var sub))
                return sub.GetString() ?? "unknown";

            return "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;

        foreach (var part in query.Split('&'))
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2)
            {
                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = Uri.UnescapeDataString(keyValue[1]);
                result[key] = value;
            }
        }
        return result;
    }

    private async void BtnSendLog_Click(object? sender, EventArgs e)
    {
        await SendLogEntryAsync();
    }

    private async void BtnSendMultiple_Click(object? sender, EventArgs e)
    {
        var count = (int)nudCount.Value;
        btnSendMultiple.Enabled = false;
        btnSendLog.Enabled = false;

        try
        {
            Log($"Sending {count} log entries...");
            var originalMessage = txtMessage.Text;

            for (int i = 1; i <= count; i++)
            {
                txtMessage.Text = $"{originalMessage} [{i}/{count}]";
                await SendLogEntryAsync();
                txtMessage.Text = originalMessage;

                if (i < count)
                    await Task.Delay(100);
            }

            Log($"Completed sending {count} log entries.");
        }
        finally
        {
            btnSendMultiple.Enabled = true;
            btnSendLog.Enabled = true;
        }
    }

    private async Task SendLogEntryAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiry)
            {
                Log("Not authenticated or token expired. Please login first.");
                MessageBox.Show("Please login first.", "Not Authenticated",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSendLog.Enabled = false;

            var loggerUrl = txtLoggerUrl.Text.Trim().TrimEnd('/');
            var endpoint = $"{loggerUrl}/api/logs";

            var logData = new
            {
                Timestamp = DateTime.UtcNow,
                Level = cboLevel.SelectedItem?.ToString() ?? "Information",
                Category = txtCategory.Text.Trim(),
                Message = txtMessage.Text.Trim(),
                Application = txtApplication.Text.Trim(),
                Environment = txtEnvironment.Text.Trim(),
                MachineName = Environment.MachineName
            };

            var request = new
            {
                Realm = txtRealm.Text.Trim(),
                Client = txtClientId.Text.Trim(),
                Environment = txtEnvironment.Text.Trim(),
                Application = txtApplication.Text.Trim(),
                LogLevel = cboLevel.SelectedItem?.ToString() ?? "Information",
                LogData = logData
            };

            Log($"Sending {request.LogLevel}: {logData.Message}");

            var response = await AuthenticatedHttp.PostAsync(endpoint, request);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Log($"Log sent successfully. Response: {responseBody}");
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Log($"Failed to send log: {response.StatusCode}");
                Log(errorBody);
            }
        }
        catch (Exception ex)
        {
            Log($"Error sending log: {ex.Message}");
        }
        finally
        {
            btnSendLog.Enabled = true;
        }
    }
}
