#Requires -RunAsAdministrator
param(
    [string]$IconPath,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = $scriptDir
$cliProjectPath = Join-Path $solutionDir "src\CodeZip.Cli\CodeZip.Cli.csproj"
$nupkgDir = Join-Path $solutionDir "nupkg"
$registryPath = "Registry::HKEY_CLASSES_ROOT\Directory\shell\CodeZip"
$commandPath = "$registryPath\command"
$defaultIcon = "shell32.dll,45"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "    [OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "    [!] $Message" -ForegroundColor Yellow
}

function Write-Failure {
    param([string]$Message)
    Write-Host "    [X] $Message" -ForegroundColor Red
}

if ($Uninstall) {
    Write-Host ""
    Write-Host "Uninstalling CodeZip..." -ForegroundColor Cyan
    Write-Host ""

    Write-Step "Removing context menu..."
    if (Test-Path $registryPath) {
        Remove-Item -Path $registryPath -Recurse -Force
        Write-Success "Context menu removed"
    } else {
        Write-Warn "Context menu not found"
    }

    Write-Step "Uninstalling dotnet tool..."
    $toolInstalled = dotnet tool list -g | Select-String "codezip"
    if ($toolInstalled) {
        dotnet tool uninstall -g CodeZip.Cli 2>$null
        Write-Success "Tool uninstalled"
    } else {
        Write-Warn "Tool not found"
    }

    Write-Host ""
    Write-Host "CodeZip uninstalled successfully!" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "    CodeZip Installation Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Step "Checking prerequisites..."

if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Failure ".NET SDK not found. Please install .NET 10 SDK."
    exit 1
}

$dotnetVersion = dotnet --version
Write-Success ".NET SDK found: $dotnetVersion"

if (-not (Test-Path $cliProjectPath)) {
    Write-Failure "Project not found at: $cliProjectPath"
    exit 1
}
Write-Success "Project found"

Write-Step "Building solution..."
dotnet build $solutionDir -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Failure "Build failed"
    exit 1
}
Write-Success "Build succeeded"

Write-Step "Creating NuGet package..."
if (Test-Path $nupkgDir) {
    Remove-Item $nupkgDir -Recurse -Force
}

dotnet pack $cliProjectPath -c Release -o $nupkgDir --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Failure "Pack failed"
    exit 1
}

$nupkgFile = Get-ChildItem $nupkgDir -Filter "*.nupkg" | Select-Object -First 1
if (-not $nupkgFile) {
    Write-Failure "NuGet package not created"
    exit 1
}
Write-Success "Package created: $($nupkgFile.Name)"

Write-Step "Installing dotnet tool..."
$existingTool = dotnet tool list -g | Select-String "codezip"
if ($existingTool) {
    Write-Host "    Removing existing installation..." -ForegroundColor Gray
    dotnet tool uninstall -g CodeZip.Cli 2>$null
}

dotnet tool install -g --add-source $nupkgDir CodeZip.Cli
if ($LASTEXITCODE -ne 0) {
    Write-Failure "Tool installation failed"
    exit 1
}
Write-Success "Tool installed"

$codeshipPath = Join-Path $env:USERPROFILE ".dotnet\tools\codeship.exe"
if (-not (Test-Path $codeshipPath)) {
    $codeshipPath = (Get-Command "codeship" -ErrorAction SilentlyContinue).Source
}

if (-not $codeshipPath -or -not (Test-Path $codeshipPath)) {
    Write-Failure "Could not locate codeship.exe after installation"
    exit 1
}
Write-Success "Verified: $codeshipPath"

Write-Step "Installing Windows Explorer context menu..."

$icon = $defaultIcon
if ($IconPath) {
    if (Test-Path $IconPath) {
        $icon = (Resolve-Path $IconPath).Path
        Write-Success "Using custom icon: $icon"
    } else {
        Write-Warn "Icon not found at $IconPath, using default"
    }
} else {
    $projectIcons = Get-ChildItem -Path (Join-Path $solutionDir "src\CodeZip.Cli") -Filter "*.ico" -ErrorAction SilentlyContinue
    if ($projectIcons) {
        $icon = $projectIcons[0].FullName
        Write-Success "Using project icon: $icon"
    }
}

if (-not (Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}
Set-ItemProperty -Path $registryPath -Name "(Default)" -Value "Create CodeZip"
Set-ItemProperty -Path $registryPath -Name "Icon" -Value $icon

if (-not (Test-Path $commandPath)) {
    New-Item -Path $commandPath -Force | Out-Null
}
Set-ItemProperty -Path $commandPath -Name "(Default)" -Value "`"$codeshipPath`" zip `"%V`""

Write-Success "Context menu installed"

Write-Step "Creating output directory..."
$outputDir = "C:\temp\CodeZipperData"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Success "Created: $outputDir"
} else {
    Write-Success "Already exists: $outputDir"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "    Installation Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Usage:" -ForegroundColor White
Write-Host "  - Right-click any folder in Explorer, select Create CodeZip" -ForegroundColor Gray
Write-Host "  - Command line: codeship zip [path]" -ForegroundColor Gray
Write-Host ""
Write-Host "Zip files saved to: $outputDir" -ForegroundColor Gray
Write-Host ""
Write-Host "To uninstall run: .\Install-CodeZip.ps1 -Uninstall" -ForegroundColor Yellow
Write-Host ""