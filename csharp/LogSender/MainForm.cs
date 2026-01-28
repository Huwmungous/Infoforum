using System.Text.Json;
using IFGlobal.Auth;
using IFGlobal.Http;

namespace LogSender;

public class MainForm : Form
{
    // Connection controls
    private GroupBox grpConnection = null!;
    private Label lblLoggerUrl = null!;
    private TextBox txtLoggerUrl = null!;
    private Label lblAuthUrl = null!;
    private TextBox txtAuthUrl = null!;
    private Label lblRealm = null!;
    private TextBox txtRealm = null!;
    private Label lblClientId = null!;
    private TextBox txtClientId = null!;
    private Label lblClientSecret = null!;
    private TextBox txtClientSecret = null!;
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
            Size = new Size(560, 175)
        };

        lblLoggerUrl = new Label { Text = "Logger URL:", Location = new Point(10, 25), Size = new Size(75, 20) };
        txtLoggerUrl = new TextBox
        {
            Location = new Point(90, 22),
            Size = new Size(455, 23),
            Text = "https://longmanrd.net/logger"
        };

        lblAuthUrl = new Label { Text = "Auth URL:", Location = new Point(10, 52), Size = new Size(75, 20) };
        txtAuthUrl = new TextBox
        {
            Location = new Point(90, 49),
            Size = new Size(455, 23),
            Text = "https://longmanrd.net/auth/realms/LongmanRd"
        };

        lblRealm = new Label { Text = "Realm:", Location = new Point(10, 79), Size = new Size(75, 20) };
        txtRealm = new TextBox
        {
            Location = new Point(90, 76),
            Size = new Size(150, 23),
            Text = "LongmanRd"
        };

        lblClientId = new Label { Text = "Client ID:", Location = new Point(260, 79), Size = new Size(60, 20) };
        txtClientId = new TextBox
        {
            Location = new Point(325, 76),
            Size = new Size(220, 23),
            Text = "infoforum-svc"
        };

        lblClientSecret = new Label { Text = "Secret:", Location = new Point(10, 106), Size = new Size(75, 20) };
        txtClientSecret = new TextBox
        {
            Location = new Point(90, 103),
            Size = new Size(280, 23),
            UseSystemPasswordChar = true
        };

        btnAuthenticate = new Button
        {
            Text = "Authenticate",
            Location = new Point(380, 102),
            Size = new Size(90, 25)
        };
        btnAuthenticate.Click += BtnAuthenticate_Click;

        lblAuthStatus = new Label
        {
            Text = "Not authenticated",
            Location = new Point(10, 140),
            Size = new Size(535, 25),
            ForeColor = Color.Gray
        };

        grpConnection.Controls.AddRange(new Control[]
        {
            lblLoggerUrl, txtLoggerUrl,
            lblAuthUrl, txtAuthUrl,
            lblRealm, txtRealm,
            lblClientId, txtClientId,
            lblClientSecret, txtClientSecret,
            btnAuthenticate, lblAuthStatus
        });

        // ============================================
        // Log Entry Group
        // ============================================
        grpLogEntry = new GroupBox
        {
            Text = "Log Entry",
            Location = new Point(12, 195),
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
            Location = new Point(12, 400),
            Size = new Size(560, 110),
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
        this.Text = "Log Sender - IFGlobal";

        this.ResumeLayout(false);
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

    private async void BtnAuthenticate_Click(object? sender, EventArgs e)
    {
        try
        {
            btnAuthenticate.Enabled = false;
            lblAuthStatus.Text = "Authenticating...";
            lblAuthStatus.ForeColor = Color.Orange;

            var authUrl = txtAuthUrl.Text.Trim();
            var clientId = txtClientId.Text.Trim();
            var clientSecret = txtClientSecret.Text.Trim();

            if (string.IsNullOrEmpty(clientSecret))
            {
                MessageBox.Show("Please enter the client secret.", "Missing Secret",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                lblAuthStatus.Text = "Not authenticated";
                lblAuthStatus.ForeColor = Color.Gray;
                return;
            }

            Log($"Authenticating as {clientId}...");

            // Use IFGlobal's ServiceAuthenticator
            _accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync(
                authUrl,
                clientId,
                clientSecret);

            // Token typically expires in 300 seconds, use 270 to be safe
            _tokenExpiry = DateTime.UtcNow.AddSeconds(270);

            // Configure AuthenticatedHttp with the token
            AuthenticatedHttp.ConfigureWithToken(_accessToken);

            Log($"Authenticated successfully. Token expires at {_tokenExpiry:HH:mm:ss}");
            lblAuthStatus.Text = $"Authenticated as {clientId}. Expires: {_tokenExpiry:HH:mm:ss}";
            lblAuthStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            Log($"Auth error: {ex.Message}");
            lblAuthStatus.Text = $"Error: {ex.Message}";
            lblAuthStatus.ForeColor = Color.Red;
            _accessToken = null;
        }
        finally
        {
            btnAuthenticate.Enabled = true;
        }
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
                Log("Not authenticated or token expired. Please authenticate first.");
                MessageBox.Show("Please authenticate first.", "Not Authenticated",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnSendLog.Enabled = false;

            var loggerUrl = txtLoggerUrl.Text.Trim().TrimEnd('/');
            var endpoint = $"{loggerUrl}/api/logs";

            // Build log data matching the IFGlobal LogData structure
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

            // Build the request matching LogEntryRequest
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

            // Use IFGlobal's AuthenticatedHttp
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
