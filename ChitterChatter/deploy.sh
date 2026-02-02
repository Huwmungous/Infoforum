#!/bin/bash
# deploy-chitterchatter.sh
# Run this on gambit to build and deploy everything

set -e

VERSION="${1:-1.0.0}"
REPO_DIR="$HOME/repos/Infoforum"
DEPLOY_DIR="/var/www/chitterchatter-dist"

echo "=========================================="
echo "ChitterChatter Build & Deploy"
echo "Version: $VERSION"
echo "=========================================="

# Build the Distribution web service (for Linux)
echo ""
echo "Building Distribution web service..."
cd "$REPO_DIR/ChitterChatter/Distribution"
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# Build the ChitterChatter client (for Windows - cross-compile from Linux)
echo ""
echo "Building ChitterChatter Windows client..."
cd "$REPO_DIR/ChitterChatter/ChitterChatterClient"
dotnet publish -c Release -r win-x64 --self-contained \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableWindowsTargeting=true \
    -p:Version=$VERSION \
    -o ./publish-win

# Create the installer zip
echo ""
echo "Creating installer package..."
cd ./publish-win

# Create Install.ps1 inline
cat > Install.ps1 << 'INSTALLSCRIPT'
# ChitterChatter Install Script - Run as Administrator
param([switch]$Silent, [switch]$NoDesktopShortcut)

$ErrorActionPreference = "Stop"
$installPath = "$env:ProgramFiles\Infoforum\ChitterChatterClient"
$startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Infoforum"
$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Status($message, $color = "White") {
    if (-not $Silent) { Write-Host $message -ForegroundColor $color }
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Status "ERROR: Run as Administrator" "Red"
    if (-not $Silent) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Status "ChitterChatter Installer" "Cyan"
Write-Status ""

$running = Get-Process -Name "ChitterChatter" -ErrorAction SilentlyContinue
if ($running) {
    Write-Status "Stopping running instance..." "Gray"
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

if (Test-Path $installPath) { Remove-Item -Path $installPath -Recurse -Force }
New-Item -Path $installPath -ItemType Directory -Force | Out-Null

Write-Status "Installing to: $installPath" "Yellow"
Get-ChildItem -Path $scriptDir -Exclude "Install.ps1" | Copy-Item -Destination $installPath -Recurse -Force
Write-Status "Files installed: OK" "Green"

if (-not (Test-Path $startMenuPath)) { New-Item -Path $startMenuPath -ItemType Directory -Force | Out-Null }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$startMenuPath\ChitterChatter.lnk")
$shortcut.TargetPath = "$installPath\ChitterChatter.exe"
$shortcut.WorkingDirectory = $installPath
$shortcut.Description = "ChitterChatter Voice Chat"
$shortcut.Save()
Write-Status "Start Menu shortcut: OK" "Green"

if (-not $NoDesktopShortcut -and -not $Silent) {
    $createDesktop = Read-Host "Create desktop shortcut? (Y/n)"
    if ($createDesktop -ne "n") {
        $ds = $shell.CreateShortcut("$desktopPath\ChitterChatter.lnk")
        $ds.TargetPath = "$installPath\ChitterChatter.exe"
        $ds.WorkingDirectory = $installPath
        $ds.Save()
        Write-Status "Desktop shortcut: OK" "Green"
    }
}

Write-Status ""
Write-Status "Installation complete!" "Green"
Write-Status "Launch from: Start Menu > Infoforum > ChitterChatter" "Cyan"

if (-not $Silent) {
    $launch = Read-Host "Launch now? (Y/n)"
    if ($launch -ne "n") { Start-Process "$installPath\ChitterChatter.exe" }
}
INSTALLSCRIPT

# Create the zip
cd "$REPO_DIR/ChitterChatter/ChitterChatterClient"
rm -f ChitterChatter-Setup.zip
zip -r ChitterChatter-Setup.zip publish-win/*
echo "$VERSION" > version.txt

# Deploy
echo ""
echo "Deploying to $DEPLOY_DIR..."

# Stop the service if running (to release file locks)
if systemctl is-active --quiet chitterchatter-dist 2>/dev/null; then
    echo "Stopping existing service..."
    sudo systemctl stop chitterchatter-dist
fi

sudo mkdir -p "$DEPLOY_DIR/dist"
sudo mkdir -p "$DEPLOY_DIR/wwwroot/images"

# Copy web service
sudo cp -r "$REPO_DIR/ChitterChatter/Distribution/publish/"* "$DEPLOY_DIR/"
sudo chmod +x "$DEPLOY_DIR/ChitterChatterDistribution"

# Set SELinux context to allow execution
if command -v chcon &> /dev/null; then
    sudo chcon -t bin_t "$DEPLOY_DIR/ChitterChatterDistribution"
fi

# Copy installer zip
sudo cp ChitterChatter-Setup.zip "$DEPLOY_DIR/dist/"
sudo cp version.txt "$DEPLOY_DIR/dist/"

# Create/update appsettings.json
sudo tee "$DEPLOY_DIR/appsettings.json" > /dev/null << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "IF": {
    "ConfigService": "http://localhost:5000",
    "AppDomain": "Infoforum"
  },
  "DistributionPath": "$DEPLOY_DIR/dist"
}
EOF

# Set permissions - run as hugh to avoid SELinux issues
sudo chown -R hugh:hugh "$DEPLOY_DIR"

# Create/update systemd service
sudo tee /etc/systemd/system/chitterchatter-dist.service > /dev/null << EOF
[Unit]
Description=ChitterChatter Distribution Service
After=network.target

[Service]
WorkingDirectory=$DEPLOY_DIR
ExecStart=$DEPLOY_DIR/ChitterChatterDistribution
Restart=always
RestartSec=10
SyslogIdentifier=chitterchatter-dist
User=hugh
SELinuxContext=unconfined_u:unconfined_r:unconfined_t:s0
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5004
EnvironmentFile=/etc/sysconfig/if-secrets

[Install]
WantedBy=multi-user.target
EOF

# Restart service
sudo systemctl daemon-reload
sudo systemctl enable chitterchatter-dist
sudo systemctl restart chitterchatter-dist

echo ""
echo "=========================================="
echo "Deployment complete!"
echo "Version: $VERSION"
echo "URL: https://longmanrd.net/infoforum/download/"
echo "=========================================="
echo ""
echo "Check status: sudo systemctl status chitterchatter-dist"
echo "View logs:    journalctl -u chitterchatter-dist -f"