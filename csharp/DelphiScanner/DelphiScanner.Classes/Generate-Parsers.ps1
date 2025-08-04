# Generate-Parsers.ps1
# Regenerates Delphi lexer/parser and rebuilds the .NET class library

$antlrVersion = "4.13.1"
$antlrJar = "antlr-$antlrVersion-complete.jar"
$antlrJarUrl = "https://www.antlr.org/download/$antlrJar"
$antlrJarPath = Join-Path $PSScriptRoot $antlrJar

$grammarDir = Join-Path $PSScriptRoot "Grammar"
$outputDir = Join-Path $grammarDir "Generated"
$delphiGrammar = Join-Path $grammarDir "Delphi.g4"
$dfmGrammar = Join-Path $grammarDir "DelphiDfm.g4"

Write-Host "=== Delphi ANTLR Parser Regeneration ===`n"

# Download the ANTLR jar if it's missing
if (!(Test-Path $antlrJarPath)) {
    Write-Host "ANTLR jar not found. Downloading $antlrJar..."
    Invoke-WebRequest -Uri $antlrJarUrl -OutFile $antlrJarPath
    Write-Host "Download complete.`n"
} else {
    Write-Host "ANTLR jar found: $antlrJar`n"
}

# Full clean of old generated C# files in Grammar folder
Write-Host "Cleaning previously generated C# files..."
Get-ChildItem -Path $grammarDir -Recurse -Include "Delphi*.cs" | Remove-Item -Force -ErrorAction SilentlyContinue

# Ensure Generated directory exists and is clean
if (Test-Path $outputDir) {
    Remove-Item "$outputDir\*.cs" -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Clean bin and obj for a fresh build
foreach ($dir in @("bin", "obj")) {
    $fullPath = Join-Path $PSScriptRoot $dir
    if (Test-Path $fullPath) {
        Write-Host "Removing build artifacts: $dir/"
        Remove-Item $fullPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Run ANTLR tool
Write-Host "Generating parsers from .g4 files..."
java -Xmx500M -cp antlr-4.13.1-complete.jar org.antlr.v4.Tool `
    -Dlanguage=CSharp `
    -visitor `
    -listener `
    -o Grammar\Generated `
    Grammar\Delphi.g4 Grammar\DelphiDfm.g4



if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Parser generation failed. Check grammar files for syntax errors."
    exit 1
}

Write-Host "`n✅ Parser generation completed successfully.`n"

# Rebuild the project
Write-Host "=== Building DelphiScanner.Classes ===`n"
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Build completed successfully."
} else {
    Write-Error "`n❌ Build failed. See errors above."
}
