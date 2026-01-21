#Requires -RunAsAdministrator
param(
    [switch]$Uninstall,
    [string]$CodeshipPath,
    [string]$IconPath
)

$ErrorActionPreference = "Stop"
$registryPath = "Registry::HKEY_CLASSES_ROOT\Directory\shell\CodeZip"
$commandPath = "$registryPath\command"

# Default icon - change this to your preferred icon path
$defaultIcon = "shell32.dll,45"

function Find-Codeship {
    if ($CodeshipPath -and (Test-Path $CodeshipPath)) { return $CodeshipPath }
    $inPath = Get-Command "codeship" -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }
    $dotnetTool = Join-Path $env:USERPROFILE ".dotnet\tools\codeship.exe"
    if (Test-Path $dotnetTool) { return $dotnetTool }
    return $null
}

if ($Uninstall) {
    if (Test-Path $registryPath) {
        Remove-Item -Path $registryPath -Recurse -Force
        Write-Host "Context menu removed." -ForegroundColor Green
    } else {
        Write-Host "Context menu not found." -ForegroundColor Yellow
    }
    exit 0
}

$codeship = Find-Codeship
if (-not $codeship) {
    Write-Host "codeship.exe not found. Install it first:" -ForegroundColor Red
    Write-Host "  dotnet tool install -g CodeZip.Cli" -ForegroundColor Yellow
    exit 1
}

# Determine icon to use
$icon = $defaultIcon
if ($IconPath) {
    if (Test-Path $IconPath) {
        $icon = $IconPath
    } else {
        Write-Host "Warning: Icon not found at '$IconPath', using default" -ForegroundColor Yellow
    }
}

Write-Host "Using codeship: $codeship" -ForegroundColor Cyan
Write-Host "Using icon: $icon" -ForegroundColor Cyan

if (-not (Test-Path $registryPath)) { New-Item -Path $registryPath -Force | Out-Null }
Set-ItemProperty -Path $registryPath -Name "(Default)" -Value "Create CodeZip"
Set-ItemProperty -Path $registryPath -Name "Icon" -Value $icon

if (-not (Test-Path $commandPath)) { New-Item -Path $commandPath -Force | Out-Null }
Set-ItemProperty -Path $commandPath -Name "(Default)" -Value "`"$codeship`" zip `"%V`""

Write-Host ""
Write-Host "Context menu installed!" -ForegroundColor Green
Write-Host "Right-click any folder to see 'Create CodeZip'" -ForegroundColor Cyan
