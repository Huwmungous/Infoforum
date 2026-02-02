using System.Collections.ObjectModel;
using System.Windows;
using ChitterChatterClient.Models;
using ChitterChatterClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChitterChatterClient.ViewModels;

/// <summary>
/// Main ViewModel for the ChitterChatter client.
/// Handles automatic authentication on startup.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private ChatterHubService? _hubService;
    private readonly AudioService _audioService;
    private readonly AuthService _authService;
    private readonly ConfigServiceClient _configService;
    private readonly SynchronizationContext? _syncContext;

    // ═══════════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _serverUrl = "Not configured";

    [ObservableProperty]
    private string _username = "Authenticating...";

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private string _statusMessage = "Initialising...";

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isDeafened;

    [ObservableProperty]
    private bool _usePushToTalk;

    [ObservableProperty]
    private float _inputLevel;

    [ObservableProperty]
    private float _inputVolume = 1.0f;

    [ObservableProperty]
    private float _outputVolume = 1.0f;

    [ObservableProperty]
    private ChatterRoom? _selectedRoom;

    [ObservableProperty]
    private ChatterUser? _selectedUser;

    [ObservableProperty]
    private string? _currentRoomId;

    [ObservableProperty]
    private bool _isInCall;

    [ObservableProperty]
    private ChatterUser? _callPartner;

    [ObservableProperty]
    private PrivateCallRequest? _incomingCall;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ChatterRoom> Rooms { get; } = [];
    public ObservableCollection<ChatterUser> Users { get; } = [];
    public ObservableCollection<ChatterUser> RoomParticipants { get; } = [];
    public ObservableCollection<string> InputDevices { get; } = [];
    public ObservableCollection<string> OutputDevices { get; } = [];

    [ObservableProperty]
    private int _selectedInputDevice;

    [ObservableProperty]
    private int _selectedOutputDevice;

    // ═══════════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════════

    public MainViewModel()
    {
        _syncContext = SynchronizationContext.Current;

        _audioService = new AudioService();
        _authService = new AuthService();
        _configService = new ConfigServiceClient();

        // Wire up auth events
        _authService.AuthenticationChanged += HandleAuthenticationChanged;

        // Wire up audio events
        _audioService.AudioCaptured += HandleAudioCaptured;
        _audioService.SpeakingStateChanged += HandleLocalSpeakingStateChanged;
        _audioService.InputLevelChanged += level => RunOnUiThread(() => InputLevel = level);
        _audioService.ErrorOccurred += HandleError;

        // Load audio devices
        LoadAudioDevices();

        // Initialise and authenticate
        _ = InitialiseAndAuthenticateAsync();
    }

    /// <summary>
    /// Initialises configuration and handles authentication.
    /// If no valid session exists, shows the login window.
    /// </summary>
    private async Task InitialiseAndAuthenticateAsync()
    {
        try
        {
            StatusMessage = "Fetching configuration...";
            var bootstrap = await _configService.GetBootstrapConfigAsync();

            if (bootstrap == null)
            {
                ErrorMessage = "Failed to fetch configuration";
                StatusMessage = "Configuration error";
                return;
            }

            // Set the authority for auth service
            var authority = bootstrap.OpenIdConfig;
            if (!authority.Contains(bootstrap.Realm))
            {
                authority = $"{authority}/{bootstrap.Realm}";
            }
            _authService.SetAuthority(authority);

            // Try to restore a previous session
            StatusMessage = "Checking authentication...";
            var sessionRestored = await _authService.TryRestoreSessionAsync();

            if (sessionRestored)
            {
                // Session restored - continue with login completion
                await CompleteLoginAsync();
            }
            else
            {
                // No valid session - need to show login
                Username = "Not logged in";
                StatusMessage = "Authentication required";
                
                // Show login window
                var success = await _authService.LoginAsync();
                
                if (success)
                {
                    await CompleteLoginAsync();
                }
                else
                {
                    // User cancelled or login failed - close the application
                    StatusMessage = "Authentication required to continue";
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Initialisation error";
        }
    }

    /// <summary>
    /// Completes the login process after successful authentication and auto-connects.
    /// </summary>
    private async Task CompleteLoginAsync()
    {
        Username = _authService.Username ?? "Unknown";
        StatusMessage = "Fetching service configuration...";

        // Fetch ChitterChatter URL from ConfigService
        var token = await _authService.GetAccessTokenAsync();
        if (token != null)
        {
            var hubUrl = await _configService.GetConfigStringAsync("chitterchatterhub", token);
            if (!string.IsNullOrEmpty(hubUrl))
            {
                ServerUrl = hubUrl;
            }
            else
            {
                // Fallback: use default URL
                ServerUrl = "https://longmanrd.net/infoforum/chitterchatter/chatter";
            }
        }

        IsLoggedIn = true;
        
        // Auto-connect to the hub
        await ConnectAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (!IsLoggedIn)
        {
            ErrorMessage = "Not authenticated";
            return;
        }

        try
        {
            ErrorMessage = null;
            StatusMessage = "Connecting...";

            // Create hub service with current URL and token provider
            if (_hubService != null)
            {
                await _hubService.DisposeAsync();
            }
            _hubService = new ChatterHubService(ServerUrl, async () => await _authService.GetAccessTokenAsync());
            WireUpHubEvents();

            // Connect with user info from auth
            await _hubService.ConnectAsync(_authService.UserId, _authService.Username);

            // Initialise audio
            _audioService.Initialise(SelectedInputDevice, SelectedOutputDevice);
            _audioService.StartCapture();

            // Load initial data
            var rooms = await _hubService.GetRoomsAsync();
            var users = await _hubService.GetOnlineUsersAsync();

            RunOnUiThread(() =>
            {
                Rooms.Clear();
                foreach (var room in rooms)
                {
                    Rooms.Add(room);
                }

                Users.Clear();
                foreach (var user in users)
                {
                    Users.Add(new ChatterUser
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        Status = user.Status,
                        RoomId = user.RoomId,
                        IsMuted = user.IsMuted,
                        IsDeafened = user.IsDeafened
                    });
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Connection failed";
        }
    }

    private void WireUpHubEvents()
    {
        if (_hubService == null) return;

        _hubService.ConnectionStateChanged += HandleConnectionStateChanged;
        _hubService.InitialStateReceived += HandleInitialStateReceived;
        _hubService.UserConnected += HandleUserConnected;
        _hubService.UserDisconnected += HandleUserDisconnected;
        _hubService.UserStatusChanged += HandleUserStatusChanged;
        _hubService.UserJoinedRoom += HandleUserJoinedRoom;
        _hubService.UserLeftRoom += HandleUserLeftRoom;
        _hubService.IncomingCall += HandleIncomingCall;
        _hubService.CallAccepted += HandleCallAccepted;
        _hubService.CallDeclined += HandleCallDeclined;
        _hubService.CallEnded += HandleCallEnded;
        _hubService.AudioReceived += HandleAudioReceived;
        _hubService.SpeakingStatusChanged += HandleSpeakingStatusChanged;
        _hubService.UserMuteChanged += HandleUserMuteChanged;
        _hubService.ErrorReceived += HandleError;
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        try
        {
            _audioService.StopCapture();
            if (_hubService != null)
            {
                await _hubService.DisconnectAsync();
            }

            RunOnUiThread(() =>
            {
                Rooms.Clear();
                Users.Clear();
                RoomParticipants.Clear();
                CurrentRoomId = null;
                IsInCall = false;
                CallPartner = null;
                StatusMessage = "Disconnected - ready to connect";
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task JoinRoomAsync(ChatterRoom? room)
    {
        if (room is null || !IsConnected || _hubService is null) return;

        try
        {
            await _hubService.JoinRoomAsync(room.RoomId);
            CurrentRoomId = room.RoomId;
            SelectedRoom = room;

            // Update participants list
            RunOnUiThread(() =>
            {
                RoomParticipants.Clear();
                foreach (var user in Users.Where(u => u.RoomId == room.RoomId))
                {
                    RoomParticipants.Add(user);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LeaveRoomAsync()
    {
        if (!IsConnected || CurrentRoomId is null || _hubService is null) return;

        try
        {
            await _hubService.LeaveRoomAsync();
            CurrentRoomId = null;
            SelectedRoom = null;

            RunOnUiThread(() => RoomParticipants.Clear());
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task CallUserAsync(ChatterUser? user)
    {
        if (user is null || !IsConnected || user.Status == UserStatus.InPrivateCall || _hubService is null) return;

        try
        {
            await _hubService.CallUserAsync(user.UserId);
            StatusMessage = $"Calling {user.Username}...";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AcceptCallAsync()
    {
        if (IncomingCall is null || _hubService is null) return;

        try
        {
            await _hubService.AcceptCallAsync(IncomingCall.FromUserId);
            IncomingCall = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeclineCallAsync()
    {
        if (IncomingCall is null || _hubService is null) return;

        try
        {
            await _hubService.DeclineCallAsync(IncomingCall.FromUserId);
            IncomingCall = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task EndCallAsync()
    {
        if (!IsInCall || _hubService is null) return;

        try
        {
            await _hubService.EndCallAsync();
            IsInCall = false;
            CallPartner = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        _audioService.SetMuted(IsMuted);

        if (IsConnected && _hubService is not null)
        {
            _ = _hubService.SetMutedAsync(IsMuted);
        }
    }

    [RelayCommand]
    private void ToggleDeafen()
    {
        IsDeafened = !IsDeafened;
        _audioService.SetDeafened(IsDeafened);

        if (IsConnected && _hubService is not null)
        {
            _ = _hubService.SetDeafenedAsync(IsDeafened);
            if (IsDeafened)
            {
                _ = _hubService.SetMutedAsync(true);
            }
        }
    }

    public void SetPushToTalk(bool active)
    {
        _audioService.SetPushToTalk(active);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleAuthenticationChanged(bool isAuthenticated)
    {
        RunOnUiThread(() =>
        {
            IsLoggedIn = isAuthenticated;
            if (!isAuthenticated)
            {
                Username = "Not logged in";
                StatusMessage = "Authentication required";
            }
        });
    }

    private void HandleConnectionStateChanged(ConnectionState state)
    {
        RunOnUiThread(() =>
        {
            ConnectionState = state;
            IsConnected = state == ConnectionState.Connected;
            StatusMessage = state switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Connected => $"Connected as {Username}",
                ConnectionState.Reconnecting => "Reconnecting...",
                ConnectionState.Failed => "Connection failed",
                _ => "Unknown"
            };
        });
    }

    private void HandleInitialStateReceived(IReadOnlyList<ChatterRoom> rooms, IReadOnlyList<ChatterUser> users)
    {
        // Data is fetched separately after connection
    }

    private void HandleUserConnected(UserStatusUpdate update)
    {
        RunOnUiThread(() =>
        {
            var existingUser = Users.FirstOrDefault(u => u.UserId == update.UserId);
            if (existingUser is null)
            {
                Users.Add(new ChatterUser
                {
                    UserId = update.UserId,
                    Username = update.Username,
                    Status = update.Status,
                    RoomId = update.RoomId,
                    IsMuted = update.IsMuted,
                    IsDeafened = update.IsDeafened
                });
            }
        });
    }

    private void HandleUserDisconnected(UserStatusUpdate update)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == update.UserId);
            if (user is not null)
            {
                Users.Remove(user);
                RoomParticipants.Remove(user);
            }
        });
    }

    private void HandleUserStatusChanged(UserStatusUpdate update)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == update.UserId);
            if (user is not null)
            {
                user.Status = update.Status;
                user.RoomId = update.RoomId;
                user.IsMuted = update.IsMuted;
                user.IsDeafened = update.IsDeafened;
            }
        });
    }

    private void HandleUserJoinedRoom(string userId, string username, string roomId)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == userId);
            if (user is not null)
            {
                user.RoomId = roomId;
                user.Status = UserStatus.InRoom;

                if (roomId == CurrentRoomId && !RoomParticipants.Contains(user))
                {
                    RoomParticipants.Add(user);
                }
            }
        });
    }

    private void HandleUserLeftRoom(string userId, string username, string roomId)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == userId);
            if (user is not null)
            {
                user.RoomId = null;
                user.Status = UserStatus.Online;
                RoomParticipants.Remove(user);
            }
        });
    }

    private void HandleIncomingCall(PrivateCallRequest request)
    {
        RunOnUiThread(() =>
        {
            IncomingCall = request;
        });
    }

    private void HandleCallAccepted(string callGroupId, string participantsJson)
    {
        RunOnUiThread(() =>
        {
            IsInCall = true;
            StatusMessage = "In call";
        });
    }

    private void HandleCallDeclined(string reason)
    {
        RunOnUiThread(() =>
        {
            StatusMessage = "Call declined";
            IncomingCall = null;
        });
    }

    private void HandleCallEnded(string endedByUserId, string reason)
    {
        RunOnUiThread(() =>
        {
            IsInCall = false;
            CallPartner = null;
            StatusMessage = IsConnected ? $"Connected as {Username}" : "Disconnected";
        });
    }

    private void HandleAudioReceived(AudioPacket packet)
    {
        _audioService.PlayAudio(packet.FromUserId, packet.AudioData);
    }

    private void HandleSpeakingStatusChanged(SpeakingStatus status)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == status.UserId);
            if (user is not null)
            {
                user.IsSpeaking = status.IsSpeaking;
            }
        });
    }

    private void HandleUserMuteChanged(string userId, bool isMuted)
    {
        RunOnUiThread(() =>
        {
            var user = Users.FirstOrDefault(u => u.UserId == userId);
            if (user is not null)
            {
                user.IsMuted = isMuted;
            }
        });
    }

    private void HandleError(string message)
    {
        RunOnUiThread(() => ErrorMessage = message);
    }

    private async void HandleAudioCaptured(byte[] audioData)
    {
        if (IsConnected && _hubService is not null && (CurrentRoomId is not null || IsInCall))
        {
            try
            {
                await _hubService.SendAudioAsync(audioData);
            }
            catch
            {
                // Ignore send errors for audio - they happen frequently during disconnects
            }
        }
    }

    private async void HandleLocalSpeakingStateChanged(bool isSpeaking)
    {
        if (IsConnected && _hubService is not null)
        {
            try
            {
                if (isSpeaking)
                {
                    await _hubService.StartSpeakingAsync();
                }
                else
                {
                    await _hubService.StopSpeakingAsync();
                }
            }
            catch
            {
                // Ignore
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private void LoadAudioDevices()
    {
        foreach (var device in AudioService.GetInputDevices())
        {
            InputDevices.Add(device);
        }

        foreach (var device in AudioService.GetOutputDevices())
        {
            OutputDevices.Add(device);
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (_syncContext is not null)
        {
            _syncContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    partial void OnInputVolumeChanged(float value)
    {
        _audioService.InputVolume = value;
    }

    partial void OnOutputVolumeChanged(float value)
    {
        _audioService.OutputVolume = value;
    }

    partial void OnUsePushToTalkChanged(bool value)
    {
        _audioService.UsePushToTalk = value;
    }

    public void Dispose()
    {
        // Fire and forget - don't block on cleanup
        // Environment.Exit(0) in App.OnExit will terminate everything
        
        try { _audioService.Dispose(); } catch { }
        try { _authService.Dispose(); } catch { }
        try { _configService.Dispose(); } catch { }
        
        // Don't wait for hub - just start the disconnect
        _ = _hubService?.DisposeAsync();
    }
}
