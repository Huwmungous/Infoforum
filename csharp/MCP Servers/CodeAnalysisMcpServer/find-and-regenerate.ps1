# find-and-regenerate.ps1
# PowerShell script to find Delphi.g4 and regenerate parser

Write-Host "========================================" -ForegroundColor Green
Write-Host "Delphi ANTLR Parser Regeneration Helper" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Search for Delphi.g4 starting from current directory
Write-Host "Searching for Delphi.g4 file..." -ForegroundColor Yellow

$grammarFiles = Get-ChildItem -Path . -Filter "Delphi.g4" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 5

if ($grammarFiles.Count -eq 0) {
    Write-Host "ERROR: Delphi.g4 not found in current directory or subdirectories!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please navigate to your project directory and try again." -ForegroundColor Yellow
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found $($grammarFiles.Count) Delphi.g4 file(s):" -ForegroundColor Green
Write-Host ""

$index = 1
foreach ($file in $grammarFiles) {
    Write-Host "[$index] $($file.FullName)" -ForegroundColor Cyan
    $index++
}
Write-Host ""

# If multiple files found, let user choose
$selectedFile = $null
if ($grammarFiles.Count -eq 1) {
    $selectedFile = $grammarFiles[0]
    Write-Host "Using: $($selectedFile.FullName)" -ForegroundColor Green
} else {
    $choice = Read-Host "Enter number (1-$($grammarFiles.Count)) to select which file to use"
    $choiceNum = [int]$choice
    if ($choiceNum -lt 1 -or $choiceNum -gt $grammarFiles.Count) {
        Write-Host "Invalid choice!" -ForegroundColor Red
        exit 1
    }
    $selectedFile = $grammarFiles[$choiceNum - 1]
    Write-Host "Selected: $($selectedFile.FullName)" -ForegroundColor Green
}

Write-Host ""

# Get the directory containing the grammar file
$grammarDir = $selectedFile.DirectoryName
Write-Host "Grammar directory: ${grammarDir}" -ForegroundColor Cyan
Write-Host ""

# Change to that directory
Push-Location $grammarDir
Write-Host "Changed to directory: $(Get-Location)" -ForegroundColor Yellow
Write-Host ""

# Check for ANTLR installation
$antlrFound = $false
$antlrCmd = ""

# Check for system ANTLR4
if (Get-Command antlr4 -ErrorAction SilentlyContinue) {
    Write-Host "Found system ANTLR4 installation" -ForegroundColor Green
    $antlrCmd = "antlr4"
    $antlrFound = $true
}
# Check for local JAR files
elseif (Test-Path "antlr-4.13.2-complete.jar") {
    Write-Host "Found local ANTLR JAR: antlr-4.13.2-complete.jar" -ForegroundColor Green
    $antlrCmd = "java -jar antlr-4.13.2-complete.jar"
    $antlrFound = $true
}
elseif (Test-Path "antlr-4.13.1-complete.jar") {
    Write-Host "Found local ANTLR JAR: antlr-4.13.1-complete.jar" -ForegroundColor Green
    $antlrCmd = "java -jar antlr-4.13.1-complete.jar"
    $antlrFound = $true
}
elseif (Test-Path "antlr-4.13.0-complete.jar") {
    Write-Host "Found local ANTLR JAR: antlr-4.13.0-complete.jar" -ForegroundColor Green
    $antlrCmd = "java -jar antlr-4.13.0-complete.jar"
    $antlrFound = $true
}

if (-not $antlrFound) {
    Write-Host "ERROR: ANTLR4 not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install ANTLR4 using one of these methods:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Option 1: Install via Chocolatey" -ForegroundColor Cyan
    Write-Host "  choco install antlr4" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Option 2: Download ANTLR JAR" -ForegroundColor Cyan
    Write-Host "  Download from: https://www.antlr.org/download/antlr-4.13.2-complete.jar" -ForegroundColor Gray
    Write-Host "  Place it in: ${grammarDir}" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Option 3: Use manual command (after downloading JAR):" -ForegroundColor Cyan
    Write-Host "  java -jar antlr-4.13.2-complete.jar -Dlanguage=CSharp Delphi.g4 -o Generated -visitor -no-listener" -ForegroundColor Gray
    Write-Host ""
    Pop-Location
    exit 1
}

# Create output directory
$outputDir = "Generated"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    Write-Host "Created output directory: ${outputDir}" -ForegroundColor Green
}

# Generate parser
Write-Host ""
Write-Host "Generating parser and lexer..." -ForegroundColor Yellow
Write-Host "Command: $antlrCmd -Dlanguage=CSharp Delphi.g4 -o ${outputDir} -visitor -no-listener" -ForegroundColor Gray
Write-Host ""

$params = @("-Dlanguage=CSharp", "Delphi.g4", "-o", $outputDir, "-visitor", "-no-listener")

if ($antlrCmd.StartsWith("java")) {
    $jarPath = $antlrCmd -replace "java -jar ", ""
    & java -jar $jarPath @params
} else {
    & antlr4 @params
}

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS! Parser generated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Generated files in ${outputDir}:" -ForegroundColor Cyan
    Get-ChildItem -Path $outputDir -Filter "*.cs" | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Copy these files to your project:" -ForegroundColor White
    Write-Host "   - ${outputDir}\DelphiLexer.cs" -ForegroundColor Gray
    Write-Host "   - ${outputDir}\DelphiParser.cs" -ForegroundColor Gray
    Write-Host "   - ${outputDir}\DelphiBaseVisitor.cs" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Replace the old files in:" -ForegroundColor White
    Write-Host "   CodeAnalysisMcpServer\Parsing\" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Build your project:" -ForegroundColor White
    Write-Host "   dotnet build" -ForegroundColor Gray
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "ERROR: Parser generation failed!" -ForegroundColor Red
    Write-Host "Please check the error messages above" -ForegroundColor Yellow
    Pop-Location
    exit 1
}

Pop-Location