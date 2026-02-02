# ChitterChatter Setup Guide

A voice chat application for InfoForum users.

## Prerequisites

### 1. WebView2 Runtime

ChitterChatter requires the Microsoft Edge WebView2 Runtime for the login screen.

**Check if already installed:**
- Open Windows Settings → Apps → Installed Apps
- Search for "WebView2"

**If not installed:**
- Download from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/
- Choose "Evergreen Bootstrapper" (small download, installs latest version)
- Run the installer

> **Note:** Most Windows 10/11 systems with Microsoft Edge installed will already have WebView2.

### 2. .NET 10.0 Runtime

**Check if already installed:**
```
dotnet --list-runtimes
```
Look for `Microsoft.WindowsDesktop.App 10.0.x`

**If not installed:**
- Download from: https://dotnet.microsoft.com/download/dotnet/10.0
- Choose ".NET Desktop Runtime 10.0" for Windows x64
- Run the installer

## Installation

### Option A: Automated Install (Recommended)

1. Extract the ChitterChatter source/release zip
2. Right-click `Install.ps1` and select **Run with PowerShell**
   - Or open PowerShell as Administrator and run: `.\Install.ps1`
3. Follow the prompts

The installer will:
- Check prerequisites
- Build the application (if needed)
- Install to `C:\Program Files\Infoforum\ChitterChatterClient`
- Create Start Menu shortcut
- Optionally create Desktop shortcut

### Option B: Manual Install

1. Build the application:
   ```
   dotnet publish ChitterChatterClient/ChitterChatterClient.csproj -c Release -o publish
   ```

2. Copy the `publish` folder contents to:
   ```
   C:\Program Files\Infoforum\ChitterChatterClient
   ```

3. Run `ChitterChatter.exe`

## First Run

1. **Launch the application**
   - From Start Menu: **Infoforum → ChitterChatter**
   - Or run `C:\Program Files\Infoforum\ChitterChatterClient\ChitterChatter.exe`

2. **Sign in**
   - A login window will appear automatically
   - Enter your InfoForum credentials
   - Click "Sign In"

3. **Grant microphone access**
   - Windows may prompt for microphone permission
   - Allow access for voice chat to work

4. **You're connected!**
   - The main window will show available rooms and online users
   - Your session is saved - next time you won't need to sign in again

## Using ChitterChatter

### Joining a Room

1. Click on a room name in the **ROOMS** panel on the left
2. Click the **Join** button
3. You'll see other participants in the room
4. Speak into your microphone - others will hear you

### Leaving a Room

1. Click the **Leave** button in the centre panel

### Audio Settings

At the bottom of the window:

- **Push to Talk**: Enable if you only want to transmit when holding Space
- **Input**: Select your microphone
- **Output**: Select your speakers/headphones

### Mute/Deafen Controls

In the centre panel when in a room:

- **Mute** (microphone icon): Others won't hear you
- **Deafen** (speaker icon): You won't hear others (also mutes you)

### Private Calls

1. Find a user in the **ONLINE USERS** panel on the right
2. Click the phone icon next to their name
3. They'll receive a call notification
4. Once accepted, you're in a private call

## Troubleshooting

### "Failed to initialise login"

- Ensure WebView2 Runtime is installed
- Check your internet connection
- Try running as Administrator

### No sound / Can't hear others

- Check the Output device selection at the bottom
- Ensure your speakers/headphones are not muted in Windows
- Check the volume slider

### Others can't hear me

- Check the Input device selection at the bottom
- Ensure Windows microphone permission is granted
- Check if you're muted (microphone icon)
- If using Push to Talk, hold Space while speaking

### Connection failed

- Check your internet connection
- The server may be temporarily unavailable
- Try closing and reopening the application

### Application won't start

- Ensure .NET 10.0 Desktop Runtime is installed
- Try running as Administrator
- Check Windows Event Viewer for error details

## Uninstalling

1. Delete the folder: `C:\Program Files\Infoforum\ChitterChatterClient`
2. Delete Start Menu shortcut: `C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Infoforum\ChitterChatter.lnk`
3. Delete Desktop shortcut (if created)
4. Optionally delete user data: `%LOCALAPPDATA%\ChitterChatter`

## Logging Out / Changing User

To sign in as a different user:

1. Close ChitterChatter
2. Navigate to `%LOCALAPPDATA%\ChitterChatter`
3. Delete the `auth.json` file
4. Restart ChitterChatter - you'll be prompted to sign in again

## Feedback & Issues

Please report any issues or feedback to the development team, including:

- What you were doing when the issue occurred
- Any error messages displayed
- Your Windows version

---

*ChitterChatter v1.0 - InfoForum*
