# Build ChitterChatter Distribution Package
# Run this on your dev machine to create the installer package

param(
    [string]$Version = "1.0.0",
    [string]$OutputPath = ".\Distribution"
)

$ErrorActionPreference = "Stop"

$projectPath = "ChitterChatterClient\ChitterChatterClient.csproj"
$publishPath = ".\publish-temp"

Write-Host "Building ChitterChatter Distribution Package" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host ""

# Clean previous builds
if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}
if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Build self-contained single-file
Write-Host "Building self-contained executable..." -ForegroundColor Yellow
dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -o $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build complete." -ForegroundColor Green
Write-Host ""

# Create distribution folder
Write-Host "Creating distribution package..." -ForegroundColor Yellow
New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null

# Copy published files
Copy-Item -Path "$publishPath\*" -Destination $OutputPath -Recurse

# Copy install script
$installScript = @"
# ChitterChatter Install Script
# Run as Administrator

param(
    [switch]`$Silent,
    [switch]`$NoDesktopShortcut
)

`$ErrorActionPreference = "Stop"

`$installPath = "`$env:ProgramFiles\Infoforum\ChitterChatterClient"
`$startMenuPath = "`$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Infoforum"
`$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
`$scriptDir = Split-Path -Parent `$MyInvocation.MyCommand.Path

function Write-Status(`$message, `$color = "White") {
    if (-not `$Silent) {
        Write-Host `$message -ForegroundColor `$color
    }
}

# Check for admin rights
`$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not `$isAdmin) {
    Write-Status "ERROR: This script must be run as Administrator." "Red"
    Write-Status "Right-click and select 'Run as Administrator'" "Yellow"
    if (-not `$Silent) { Read-Host "Press Enter to exit" }
    exit 1
}

Write-Status "ChitterChatter Installer v$Version" "Cyan"
Write-Status "===============================" "Cyan"
Write-Status ""

# Check for WebView2
Write-Status "Checking prerequisites..." "Yellow"
`$webview2 = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
if (-not `$webview2) {
    `$webview2 = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
}
if (-not `$webview2) {
    Write-Status "  WebView2 Runtime: Not detected (may still work)" "Yellow"
} else {
    Write-Status "  WebView2 Runtime: OK" "Green"
}

# Check the distribution contains the executable
`$exePath = Join-Path `$scriptDir "ChitterChatter.exe"
if (-not (Test-Path `$exePath)) {
    Write-Status "ERROR: ChitterChatter.exe not found." "Red"
    if (-not `$Silent) { Read-Host "Press Enter to exit" }
    exit 1
}
Write-Status "  Distribution files: OK" "Green"

Write-Status ""
Write-Status "Installing to: `$installPath" "Yellow"

# Stop running instance
`$running = Get-Process -Name "ChitterChatter" -ErrorAction SilentlyContinue
if (`$running) {
    Write-Status "  Stopping running instance..." "Gray"
    `$running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# Remove previous installation
if (Test-Path `$installPath) {
    Write-Status "  Removing previous installation..." "Gray"
    Remove-Item -Path `$installPath -Recurse -Force
}

New-Item -Path `$installPath -ItemType Directory -Force | Out-Null

# Copy files
Write-Status "  Copying files..." "Gray"
Get-ChildItem -Path `$scriptDir -Exclude "Install.ps1", "SETUP.md", "README.md" | 
    Copy-Item -Destination `$installPath -Recurse -Force

Write-Status "  Files installed: OK" "Green"

# Create Start Menu shortcut
Write-Status "  Creating Start Menu shortcut..." "Gray"
if (-not (Test-Path `$startMenuPath)) {
    New-Item -Path `$startMenuPath -ItemType Directory -Force | Out-Null
}

`$shell = New-Object -ComObject WScript.Shell
`$shortcut = `$shell.CreateShortcut("`$startMenuPath\ChitterChatter.lnk")
`$shortcut.TargetPath = "`$installPath\ChitterChatter.exe"
`$shortcut.WorkingDirectory = `$installPath
`$shortcut.Description = "ChitterChatter Voice Chat"
`$shortcut.Save()

Write-Status "  Start Menu shortcut: OK" "Green"

# Desktop shortcut
if (-not `$NoDesktopShortcut) {
    if (`$Silent) {
        `$createDesktop = "Y"
    } else {
        Write-Status ""
        `$createDesktop = Read-Host "Create desktop shortcut? (Y/n)"
    }
    
    if (`$createDesktop -ne "n" -and `$createDesktop -ne "N") {
        `$desktopShortcut = `$shell.CreateShortcut("`$desktopPath\ChitterChatter.lnk")
        `$desktopShortcut.TargetPath = "`$installPath\ChitterChatter.exe"
        `$desktopShortcut.WorkingDirectory = `$installPath
        `$desktopShortcut.Description = "ChitterChatter Voice Chat"
        `$desktopShortcut.Save()
        Write-Status "  Desktop shortcut: OK" "Green"
    }
}

Write-Status ""
Write-Status "Installation complete!" "Green"
Write-Status ""
Write-Status "Launch from: Start Menu > Infoforum > ChitterChatter" "Cyan"

if (-not `$Silent) {
    Write-Status ""
    `$launch = Read-Host "Launch ChitterChatter now? (Y/n)"
    if (`$launch -ne "n" -and `$launch -ne "N") {
        Start-Process "`$installPath\ChitterChatter.exe"
    }
}
"@

$installScript | Out-File -FilePath "$OutputPath\Install.ps1" -Encoding UTF8

# Create version file
$Version | Out-File -FilePath "$OutputPath\version.txt" -Encoding UTF8 -NoNewline

# Create the zip
Write-Host "Creating zip archive..." -ForegroundColor Yellow
$zipPath = ".\ChitterChatter-Setup.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath -CompressionLevel Optimal

# Clean up
Remove-Item -Path $publishPath -Recurse -Force

$zipInfo = Get-Item $zipPath
Write-Host ""
Write-Host "Distribution package created:" -ForegroundColor Green
Write-Host "  $($zipInfo.FullName)" -ForegroundColor White
Write-Host "  Size: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Copy ChitterChatter-Setup.zip to your distribution server" -ForegroundColor White
Write-Host "  2. Place in the 'dist' folder of ChitterChatterDistribution service" -ForegroundColor White
Write-Host "  3. Users can download from the protected web page" -ForegroundColor White
