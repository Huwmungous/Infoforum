#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete ANTLR setup for CodeAnalysisMcpServer - Clean install from scratch
.DESCRIPTION
    This script:
    - Cleans all previous ANTLR artifacts
    - Downloads ANTLR 4.11.1 if needed
    - Downloads Delphi.g4 grammar if needed
    - Generates C# parser files
    - Configures correct NuGet packages
    - Builds and verifies the project
.NOTES
    Run this from the CodeAnalysisMcpServer project directory
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Colors for output
function Write-Step { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }
function Write-Failure { param([string]$Message) Write-Host "✗ $Message" -ForegroundColor Red }

Write-Host @"
╔════════════════════════════════════════════════════════════════════╗
║                                                                    ║
║        ANTLR Integration Setup - Clean Install                    ║
║        CodeAnalysisMcpServer                                       ║
║                                                                    ║
╚════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Verify we're in the right directory
if (-not (Test-Path "CodeAnalysisMcpServer.csproj")) {
    Write-Failure "CodeAnalysisMcpServer.csproj not found!"
    Write-Host "Please run this script from the CodeAnalysisMcpServer project directory."
    exit 1
}

Write-Success "Found CodeAnalysisMcpServer.csproj"

# ==============================================================================
# STEP 1: CLEAN ALL PREVIOUS ARTIFACTS
# ==============================================================================
Write-Step "Step 1: Cleaning previous ANTLR artifacts..."

$itemsToClean = @(
    "Generated",
    "Parsing", 
    "bin",
    "obj",
    "*.g4.bak",
    "*.g4.old",
    "*_backup",
    "antlr-*.jar"
)

foreach ($item in $itemsToClean) {
    if (Test-Path $item) {
        Write-Host "  Removing: $item"
        Remove-Item -Path $item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Remove any ANTLR packages to start fresh
Write-Host "  Removing old ANTLR NuGet packages..."
dotnet remove package Antlr4.Runtime.Standard 2>$null

Write-Success "Cleanup complete"

# ==============================================================================
# STEP 2: CHECK PREREQUISITES - JAVA
# ==============================================================================
Write-Step "Step 2: Checking Java installation..."

# Function to test if Java is available
function Test-JavaInstalled {
    try {
        $null = java -version 2>&1
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

# Function to check if running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Check if Java is already installed
if (Test-JavaInstalled) {
    $javaVersion = java -version 2>&1 | Select-Object -First 1
    Write-Success "Java found: $javaVersion"
} else {
    Write-Warning "Java not found - installing automatically..."
    
    # Check for admin rights
    $isAdmin = Test-Administrator
    
    if (-not $isAdmin) {
        Write-Host @"
        
This script needs to install Java, which requires administrator privileges.
Please choose an option:

1. Re-run this script as Administrator (recommended)
   Right-click Setup-ANTLR-Clean.bat → "Run as administrator"

2. Install Java manually from: https://adoptium.net/
   Then run this script again.

"@
        exit 1
    }
    
    Write-Host "  Administrator privileges detected - proceeding with Java installation..."
    Write-Host ""
    
    # Try Method 1: Chocolatey (if available)
    $chocoInstalled = $null -ne (Get-Command choco -ErrorAction SilentlyContinue)
    
    if ($chocoInstalled) {
        Write-Host "  Method 1: Installing Java via Chocolatey..."
        try {
            choco install temurin17 -y --no-progress
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Chocolatey installation completed successfully"
                
                # Refresh environment variables more aggressively
                $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
                $env:JAVA_HOME = [System.Environment]::GetEnvironmentVariable("JAVA_HOME","Machine")
                
                # Test if Java is now available
                if (Test-JavaInstalled) {
                    $javaVersion = java -version 2>&1 | Select-Object -First 1
                    Write-Success "Java installed and verified: $javaVersion"
                } else {
                    Write-Warning "Java installed by Chocolatey but not yet available in current session."
                    Write-Host @"
                    
Java has been successfully installed by Chocolatey, but environment variables
are not yet visible in the current PowerShell session.

SOLUTION 1 (Recommended):
  Close this PowerShell window and run the script again.
  Java will be detected and setup will complete.

SOLUTION 2 (Quick):
  In this same window, run these commands:
  
  `$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine")
  java -version
  .\Setup-ANTLR-Clean.ps1
  
This is a Windows PowerShell limitation - environment variables from new
installations don't appear in already-running sessions.

Java IS installed successfully at:
  C:\Program Files\Eclipse Adoptium\jdk-17.*.*-hotspot\

"@
                    exit 0  # Exit gracefully - Java is installed, just restart needed
                }
            } else {
                throw "Chocolatey installation failed with exit code: $LASTEXITCODE"
            }
        } catch {
            Write-Warning "Chocolatey installation had issues: $($_.Exception.Message)"
            Write-Host "  Trying alternative method..."
        }
    }
    
    # Try Method 2: Direct download and install (if Chocolatey failed or unavailable)
    if (-not (Test-JavaInstalled)) {
        Write-Host "  Method 2: Downloading and installing Java directly..."
        
        # Eclipse Temurin (AdoptiumJDK) - MSI installer
        $javaInstaller = "OpenJDK17U-jdk_x64_windows_hotspot.msi"
        $javaUrl = "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.9%2B9/OpenJDK17U-jdk_x64_windows_hotspot_17.0.9_9.msi"
        
        try {
            Write-Host "  Downloading Java JDK 17 from Adoptium..."
            Write-Host "  This may take a few minutes (approximately 160 MB)..."
            
            # Download with progress
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri $javaUrl -OutFile $javaInstaller -UseBasicParsing
            $ProgressPreference = 'Continue'
            
            Write-Host "  Installing Java..."
            $installArgs = @(
                "/i",
                $javaInstaller,
                "/quiet",
                "/norestart",
                "ADDLOCAL=FeatureMain,FeatureEnvironment,FeatureJarFileRunWith,FeatureJavaHome",
                "INSTALLDIR=C:\Program Files\Eclipse Adoptium\jdk-17.0.9.9-hotspot\"
            )
            
            $process = Start-Process -FilePath "msiexec.exe" -ArgumentList $installArgs -Wait -PassThru -NoNewWindow
            
            if ($process.ExitCode -eq 0) {
                Write-Host "  Java installed successfully!"
                
                # Clean up installer
                Remove-Item $javaInstaller -Force -ErrorAction SilentlyContinue
                
                # Refresh environment variables
                $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
                $env:JAVA_HOME = [System.Environment]::GetEnvironmentVariable("JAVA_HOME","Machine")
                
                # Test again
                if (Test-JavaInstalled) {
                    $javaVersion = java -version 2>&1 | Select-Object -First 1
                    Write-Success "Java installed and configured: $javaVersion"
                } else {
                    Write-Warning "Java installed but not yet available in current session."
                    Write-Host "  Please restart your PowerShell/terminal and run this script again."
                    exit 1
                }
            } else {
                throw "MSI installation failed with exit code: $($process.ExitCode)"
            }
        } catch {
            Write-Failure "Automatic Java installation failed: $($_.Exception.Message)"
            Write-Host @"

Please install Java manually:
1. Download from: https://adoptium.net/
2. Install the MSI package
3. Restart your terminal
4. Run this script again

Or install via Chocolatey:
1. Install Chocolatey: https://chocolatey.org/install
2. Run: choco install temurin17 -y
3. Run this script again

"@
            exit 1
        }
    }
}

# Check dotnet
Write-Host "`n  Checking .NET SDK..."
try {
    $dotnetVersion = dotnet --version
    Write-Success ".NET SDK found: $dotnetVersion"
} catch {
    Write-Failure ".NET SDK not found!"
    Write-Host @"
    
.NET SDK is required but not found. This is unexpected as the project requires it.
Please install .NET SDK from: https://dotnet.microsoft.com/download
Then run this script again.
"@
    exit 1
}

# ==============================================================================
# STEP 3: DOWNLOAD ANTLR 4.11.1
# ==============================================================================
Write-Step "Step 3: Downloading ANTLR 4.11.1..."

$antlrJar = "antlr-4.11.1-complete.jar"
$antlrUrl = "https://www.antlr.org/download/antlr-4.11.1-complete.jar"

if (-not (Test-Path $antlrJar)) {
    Write-Host "  Downloading from $antlrUrl..."
    try {
        Invoke-WebRequest -Uri $antlrUrl -OutFile $antlrJar -UseBasicParsing
        Write-Success "Downloaded $antlrJar"
    } catch {
        Write-Failure "Failed to download ANTLR!"
        Write-Host "Please download manually from: $antlrUrl"
        exit 1
    }
} else {
    Write-Success "$antlrJar already present"
}

# ==============================================================================
# STEP 4: VERIFY DELPHI.G4 GRAMMAR EXISTS
# ==============================================================================
Write-Step "Step 4: Verifying Delphi.g4 grammar..."

$grammarFile = "Delphi.g4"

if (-not (Test-Path $grammarFile)) {
    Write-Failure "$grammarFile not found!"
    Write-Host @"
    
The Delphi.g4 grammar file is required but not found in the project directory.
This script does NOT download the grammar because you have a MODIFIED version.

Please ensure your modified Delphi.g4 file is in:
    $(Get-Location)\$grammarFile

If you don't have it, you may need to:
1. Restore it from your backups
2. Get it from your source control
3. Or download the standard version from:
   https://raw.githubusercontent.com/antlr/grammars-v4/master/delphi/delphi.g4
   (but remember you have modifications!)

"@
    exit 1
}

Write-Success "Found $grammarFile (using your modified version)"

# Show file info to confirm it's the right one
$grammarInfo = Get-Item $grammarFile
Write-Host "  File size: $($grammarInfo.Length) bytes"
Write-Host "  Modified: $($grammarInfo.LastWriteTime)"

# ==============================================================================
# STEP 5: ADD NUGET PACKAGE
# ==============================================================================
Write-Step "Step 5: Installing ANTLR Runtime NuGet package..."

try {
    dotnet add package Antlr4.Runtime.Standard --version 4.11.1
    Write-Success "Antlr4.Runtime.Standard 4.11.1 installed"
} catch {
    Write-Failure "Failed to add NuGet package"
    exit 1
}

# ==============================================================================
# STEP 6: CREATE GENERATED FOLDER
# ==============================================================================
Write-Step "Step 6: Creating Generated folder..."

New-Item -Path "Generated" -ItemType Directory -Force | Out-Null
Write-Success "Generated folder created"

# ==============================================================================
# STEP 7: GENERATE C# PARSER
# ==============================================================================
Write-Step "Step 7: Generating C# parser from Delphi.g4..."

Write-Host "  This may take 30-60 seconds for the full grammar..."
Write-Host "  Running: java -jar $antlrJar -Dlanguage=CSharp ..."

try {
    $antlrArgs = @(
        "-jar", $antlrJar,
        "-Dlanguage=CSharp",
        "-visitor",
        "-no-listener", 
        "-package", "CodeAnalysisMcpServer.Generated",
        "-o", "Generated",
        $grammarFile
    )
    
    $process = Start-Process -FilePath "java" -ArgumentList $antlrArgs -Wait -NoNewWindow -PassThru
    
    if ($process.ExitCode -ne 0) {
        throw "ANTLR generation failed with exit code $($process.ExitCode)"
    }
    
    Write-Success "Parser generated successfully"
} catch {
    Write-Failure "Parser generation failed!"
    Write-Host $_.Exception.Message
    exit 1
}

# Verify generated files
$expectedFiles = @(
    "Generated/DelphiLexer.cs",
    "Generated/DelphiParser.cs",
    "Generated/DelphiBaseVisitor.cs",
    "Generated/DelphiVisitor.cs"
)

Write-Host "`n  Verifying generated files:"
$allFilesPresent = $true
foreach ($file in $expectedFiles) {
    if (Test-Path $file) {
        Write-Host "    ✓ $file" -ForegroundColor Green
    } else {
        Write-Host "    ✗ $file (MISSING!)" -ForegroundColor Red
        $allFilesPresent = $false
    }
}

if (-not $allFilesPresent) {
    Write-Failure "Some generated files are missing!"
    exit 1
}

# ==============================================================================
# STEP 8: UPDATE ANTLRDELPHISQLEXTRACTOR.CS
# ==============================================================================
Write-Step "Step 8: Updating AntlrDelphiSqlExtractor.cs..."

$extractorPath = "Tools/AntlrDelphiSqlExtractor.cs"

if (Test-Path $extractorPath) {
    $content = Get-Content $extractorPath -Raw
    
    # Check if it has the wrong namespace or class names
    $needsUpdate = $false
    
    if ($content -match "using CodeAnalysisMcpServer\.Parsing;") {
        Write-Host "  Fixing namespace: Parsing -> Generated"
        $content = $content -replace "using CodeAnalysisMcpServer\.Parsing;", "using CodeAnalysisMcpServer.Generated;"
        $needsUpdate = $true
    }
    
    if ($content -match "DelphiSqlLexer") {
        Write-Host "  Fixing class name: DelphiSqlLexer -> DelphiLexer"
        $content = $content -replace "DelphiSqlLexer", "DelphiLexer"
        $needsUpdate = $true
    }
    
    if ($content -match "DelphiSqlParser") {
        Write-Host "  Fixing class name: DelphiSqlParser -> DelphiParser"
        $content = $content -replace "DelphiSqlParser", "DelphiParser"
        $needsUpdate = $true
    }
    
    if ($content -match "DelphiSqlBaseVisitor") {
        Write-Host "  Fixing class name: DelphiSqlBaseVisitor -> DelphiBaseVisitor"
        $content = $content -replace "DelphiSqlBaseVisitor", "DelphiBaseVisitor"
        $needsUpdate = $true
    }
    
    if ($needsUpdate) {
        Set-Content -Path $extractorPath -Value $content -NoNewline
        Write-Success "AntlrDelphiSqlExtractor.cs updated"
    } else {
        Write-Success "AntlrDelphiSqlExtractor.cs already correct"
    }
} else {
    Write-Warning "AntlrDelphiSqlExtractor.cs not found at $extractorPath"
    Write-Host "  You may need to create this file manually"
}

# ==============================================================================
# STEP 9: BUILD PROJECT
# ==============================================================================
Write-Step "Step 9: Building project..."

Write-Host "  Running: dotnet clean"
dotnet clean | Out-Null

Write-Host "  Running: dotnet build"
$buildOutput = dotnet build 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Success "Build succeeded!"
} else {
    Write-Failure "Build failed!"
    Write-Host "`nBuild output:"
    Write-Host $buildOutput
    Write-Host "`nPlease review the errors above."
    exit 1
}

# ==============================================================================
# STEP 10: VERIFY EVERYTHING
# ==============================================================================
Write-Step "Step 10: Final verification..."

$checksums = @{
    "Java" = (java -version 2>&1 | Select-Object -First 1)
    "ANTLR JAR" = (Test-Path $antlrJar)
    "Grammar File" = "Using your modified Delphi.g4"
    "Generated Files" = (Test-Path "Generated/DelphiLexer.cs")
    "NuGet Package" = "Antlr4.Runtime.Standard 4.11.1"
    "Build Status" = "Success"
}

foreach ($check in $checksums.GetEnumerator()) {
    Write-Host "  $($check.Key): " -NoNewline
    Write-Host $check.Value -ForegroundColor Green
}

# ==============================================================================
# SUCCESS!
# ==============================================================================
Write-Host @"

╔════════════════════════════════════════════════════════════════════╗
║                                                                    ║
║                    ✓ SETUP COMPLETE!                              ║
║                                                                    ║
╚════════════════════════════════════════════════════════════════════╝

What was done:
  ✓ Cleaned all previous ANTLR artifacts
  ✓ Downloaded ANTLR 4.11.1
  ✓ Used your modified Delphi.g4 grammar (not downloaded)
  ✓ Generated C# parser files in Generated/
  ✓ Installed Antlr4.Runtime.Standard 4.11.1
  ✓ Updated AntlrDelphiSqlExtractor.cs (if needed)
  ✓ Built project successfully

Generated files:
  • Generated/DelphiLexer.cs
  • Generated/DelphiParser.cs
  • Generated/DelphiBaseVisitor.cs
  • Generated/DelphiVisitor.cs

Next steps:
  1. Open solution in Visual Studio
  2. Verify the Generated folder is included in the project
  3. Build in Visual Studio (should be clean, zero warnings)
  4. Run the MCP server
  5. Test ANTLR-based SQL extraction

Usage in tools:
  method='antlr'  - Use ANTLR parser (robust, recommended)
  method='regex'  - Use original regex parser (fallback)
  method='both'   - Compare both methods side-by-side

"@ -ForegroundColor Green

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")