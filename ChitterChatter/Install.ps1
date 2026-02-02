# ChitterChatter Install Script
# Run as Administrator

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

$installPath = "$env:ProgramFiles\Infoforum\ChitterChatterClient"
$startMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Infoforum"
$desktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")

Write-Host "ChitterChatter Installer" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Check for .NET 10.0
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
$dotnetRuntimes = dotnet --list-runtimes 2>$null
if ($dotnetRuntimes -notmatch "Microsoft\.WindowsDesktop\.App 10\.0") {
    Write-Host "ERROR: .NET 10.0 Desktop Runtime not found." -ForegroundColor Red
    Write-Host "Please download and install from:" -ForegroundColor Red
    Write-Host "https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host "  .NET 10.0 Desktop Runtime: OK" -ForegroundColor Green

# Check for WebView2
$webview2 = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
if (-not $webview2) {
    $webview2 = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue
}
if (-not $webview2) {
    Write-Host "WARNING: WebView2 Runtime not detected." -ForegroundColor Yellow
    Write-Host "It may still be installed. If login fails, please install from:" -ForegroundColor Yellow
    Write-Host "https://developer.microsoft.com/en-us/microsoft-edge/webview2/" -ForegroundColor Yellow
} else {
    Write-Host "  WebView2 Runtime: OK" -ForegroundColor Green
}

Write-Host ""

# Find the built executable
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDir = $null

# Check for published output first
$possiblePaths = @(
    "$scriptDir\publish",
    "$scriptDir\ChitterChatterClient\bin\Release\net10.0-windows\publish",
    "$scriptDir\ChitterChatterClient\bin\Release\net10.0-windows",
    "$scriptDir\ChitterChatterClient\bin\Debug\net10.0-windows"
)

foreach ($path in $possiblePaths) {
    if (Test-Path "$path\ChitterChatter.exe") {
        $publishDir = $path
        break
    }
}

if (-not $publishDir) {
    Write-Host "Building ChitterChatter..." -ForegroundColor Yellow
    
    $slnPath = Get-ChildItem -Path $scriptDir -Filter "*.sln" -Recurse | Select-Object -First 1
    if (-not $slnPath) {
        Write-Host "ERROR: Could not find solution file." -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    
    Push-Location (Split-Path $slnPath.FullName -Parent)
    try {
        dotnet publish ChitterChatterClient/ChitterChatterClient.csproj -c Release -o "$scriptDir\publish"
        $publishDir = "$scriptDir\publish"
    }
    finally {
        Pop-Location
    }
    
    if (-not (Test-Path "$publishDir\ChitterChatter.exe")) {
        Write-Host "ERROR: Build failed." -ForegroundColor Red
        Read-Host "Press Enter to exit"
        exit 1
    }
    Write-Host "  Build: OK" -ForegroundColor Green
}

Write-Host ""
Write-Host "Installing to: $installPath" -ForegroundColor Yellow

# Create install directory
if (Test-Path $installPath) {
    Write-Host "  Removing previous installation..." -ForegroundColor Gray
    Remove-Item -Path $installPath -Recurse -Force
}

New-Item -Path $installPath -ItemType Directory -Force | Out-Null

# Copy files
Write-Host "  Copying files..." -ForegroundColor Gray
Copy-Item -Path "$publishDir\*" -Destination $installPath -Recurse -Force

Write-Host "  Files installed: OK" -ForegroundColor Green

# Create Start Menu shortcut
Write-Host "  Creating Start Menu shortcut..." -ForegroundColor Gray
if (-not (Test-Path $startMenuPath)) {
    New-Item -Path $startMenuPath -ItemType Directory -Force | Out-Null
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$startMenuPath\ChitterChatter.lnk")
$shortcut.TargetPath = "$installPath\ChitterChatter.exe"
$shortcut.WorkingDirectory = $installPath
$shortcut.Description = "ChitterChatter Voice Chat"
$shortcut.Save()

Write-Host "  Start Menu shortcut: OK" -ForegroundColor Green

# Ask about desktop shortcut
Write-Host ""
$createDesktop = Read-Host "Create desktop shortcut? (Y/n)"
if ($createDesktop -ne "n" -and $createDesktop -ne "N") {
    $desktopShortcut = $shell.CreateShortcut("$desktopPath\ChitterChatter.lnk")
    $desktopShortcut.TargetPath = "$installPath\ChitterChatter.exe"
    $desktopShortcut.WorkingDirectory = $installPath
    $desktopShortcut.Description = "ChitterChatter Voice Chat"
    $desktopShortcut.Save()
    Write-Host "  Desktop shortcut: OK" -ForegroundColor Green
}

Write-Host ""
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "ChitterChatter has been installed to:" -ForegroundColor Cyan
Write-Host "  $installPath" -ForegroundColor White
Write-Host ""
Write-Host "You can launch it from:" -ForegroundColor Cyan
Write-Host "  - Start Menu > Infoforum > ChitterChatter" -ForegroundColor White
if ($createDesktop -ne "n" -and $createDesktop -ne "N") {
    Write-Host "  - Desktop shortcut" -ForegroundColor White
}
Write-Host ""

$launch = Read-Host "Launch ChitterChatter now? (Y/n)"
if ($launch -ne "n" -and $launch -ne "N") {
    Start-Process "$installPath\ChitterChatter.exe"
}
