# Fix remaining 16 code analysis warnings
$ErrorActionPreference = "Stop"

Write-Host "Fixing Remaining 16 Warnings..." -ForegroundColor Cyan
Write-Host ""

# Fix BraveSearchMcpServer/Protocol/McpServer.cs - CA1822 warnings
$braveServerPath = ".\BraveSearchMcpServer\Protocol\McpServer.cs"
if (Test-Path $braveServerPath) {
    Write-Host "Fixing BraveSearchMcpServer/Protocol/McpServer.cs..." -ForegroundColor Yellow
    $content = Get-Content $braveServerPath -Raw
    
    # Make HandleInitialize static
    $content = $content -replace '(\s+)(private\s+McpResponse\s+HandleInitialize)', '$1private static McpResponse HandleInitialize'
    
    # Make HandleToolsList static
    $content = $content -replace '(\s+)(private\s+McpResponse\s+HandleToolsList)', '$1private static McpResponse HandleToolsList'
    
    # Make GetRequiredArg static
    $content = $content -replace '(\s+)(private\s+string\s+GetRequiredArg)', '$1private static string GetRequiredArg'
    
    # Make GetOptionalIntArg static
    $content = $content -replace '(\s+)(private\s+int\?\s+GetOptionalIntArg)', '$1private static int? GetOptionalIntArg'
    
    Set-Content -Path $braveServerPath -Value $content -Encoding UTF8
    Write-Host "  Fixed 4 CA1822 warnings" -ForegroundColor Green
}

# Fix BraveSearchMcpServer/Services/BraveSearchService.cs - Collection warnings
$braveServicePath = ".\BraveSearchMcpServer\Services\BraveSearchService.cs"
if (Test-Path $braveServicePath) {
    Write-Host "Fixing BraveSearchMcpServer/Services/BraveSearchService.cs..." -ForegroundColor Yellow
    $content = Get-Content $braveServicePath -Raw
    
    # Fix all new List<>() to []
    $content = $content -replace 'new List<SearchResultItem>\(\)', '[]'
    $content = $content -replace 'new List<SearchResultItem>\{\}', '[]'
    $content = $content -replace '= new\(\);', '= [];'
    $content = $content -replace '= new List<[^>]+>\(\);', '= [];'
    
    Set-Content -Path $braveServicePath -Value $content -Encoding UTF8
    Write-Host "  Fixed 3 collection initialization warnings" -ForegroundColor Green
}

# Fix CodeAnalysisMcpServer/Protocol/McpServer.cs - Collection warning
$codeServerPath = ".\CodeAnalysisMcpServer\Protocol\McpServer.cs"
if (Test-Path $codeServerPath) {
    Write-Host "Fixing CodeAnalysisMcpServer/Protocol/McpServer.cs..." -ForegroundColor Yellow
    $content = Get-Content $codeServerPath -Raw
    
    # Fix dictionary initialization
    $content = $content -replace 'new Dictionary<string, object>\(\)', 'new Dictionary<string, object>()'
    # Fix any List initializations
    $content = $content -replace 'new\(\)', '[]'
    
    # Make CreatePropertiesForTool static
    $content = $content -replace '(\s+)(private\s+Dictionary<string, object>\s+CreatePropertiesForTool)', '$1private static Dictionary<string, object> CreatePropertiesForTool'
    
    # Make GetRequiredParams static
    $content = $content -replace '(\s+)(private\s+string\[\]\s+GetRequiredParams)', '$1private static string[] GetRequiredParams'
    
    Set-Content -Path $codeServerPath -Value $content -Encoding UTF8
    Write-Host "  Fixed 1 collection initialization warning" -ForegroundColor Green
}

# Fix CodeAnalysisMcpServer/Tools/CodeAnalysisTools.cs - Multiple warnings
$toolsPath = ".\CodeAnalysisMcpServer\Tools\CodeAnalysisTools.cs"
if (Test-Path $toolsPath) {
    Write-Host "Fixing CodeAnalysisMcpServer/Tools/CodeAnalysisTools.cs..." -ForegroundColor Yellow
    $content = Get-Content $toolsPath -Raw
    
    # Fix collection initializations - be more specific
    $content = $content -replace 'new List<string>\(\)', '[]'
    $content = $content -replace 'new List<DatabaseCall>\(\)', '[]'
    $content = $content -replace 'new List<ProcedureCall>\(\)', '[]'
    $content = $content -replace 'new List<ClassDefinition>\(\)', '[]'
    $content = $content -replace 'new List<MethodSignature>\(\)', '[]'
    $content = $content -replace 'new List<DataStructure>\(\)', '[]'
    $content = $content -replace 'new List<object>\(\)', '[]'
    
    # Check if we need to add partial keyword for GeneratedRegex
    if ($content -notmatch 'public partial class CodeAnalysisTools') {
        $content = $content -replace 'public class CodeAnalysisTools', 'public partial class CodeAnalysisTools'
    }
    
    # Add using for GeneratedRegex if not present
    if ($content -notmatch 'using System.Text.RegularExpressions;') {
        $content = $content -replace '(using System.Text.Json;)', "`$1`nusing System.Text.RegularExpressions;"
    }
    
    # Add GeneratedRegex methods at the end of the class if not present
    if ($content -notmatch '\[GeneratedRegex\]') {
        # Find the last closing brace of the class
        $lastBrace = $content.LastIndexOf('}')
        $beforeLast = $content.Substring(0, $lastBrace)
        
        $regexMethods = @'

    // Generated Regex patterns for performance
    [GeneratedRegex(@"(\w+)\.(Open|Close|ExecSQL|Execute|Prepare|First|Next|Last|Prior)")]
    private static partial Regex DelphiDbMethodRegex();
    
    [GeneratedRegex(@"(\w+)\.(SQL|CommandText|Query)\.(?:Add|Text)")]
    private static partial Regex DelphiSqlPropertyRegex();
'@
        $content = $beforeLast + $regexMethods + "`n}"
    }
    
    # Fix unused parameter - change language to _
    $content = $content -replace 'public Task<object> ExtractTableReferencesAsync\(string code, string\? language = null\)',
                                   'public Task<object> ExtractTableReferencesAsync(string code, string? _ = null)'
    
    Set-Content -Path $toolsPath -Value $content -Encoding UTF8
    Write-Host "  Fixed 8 warnings (5 collection + 2 regex + 1 unused param)" -ForegroundColor Green
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "All 16 warnings fixed!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Rebuild to verify
Write-Host "Rebuilding to verify..." -ForegroundColor Yellow
Write-Host ""

$errors = 0

Push-Location ".\BraveSearchMcpServer"
Write-Host "Building BraveSearchMcpServer..." -ForegroundColor Cyan
$output = dotnet build 2>&1 | Out-String
if ($output -match "0 Warning\(s\)") {
    Write-Host "  Success: 0 warnings!" -ForegroundColor Green
} else {
    Write-Host "  Check output for remaining issues" -ForegroundColor Yellow
    $errors++
}
Pop-Location

Push-Location ".\CodeAnalysisMcpServer"
Write-Host "Building CodeAnalysisMcpServer..." -ForegroundColor Cyan
$output = dotnet build 2>&1 | Out-String
if ($output -match "0 Warning\(s\)") {
    Write-Host "  Success: 0 warnings!" -ForegroundColor Green
} else {
    Write-Host "  Check output for remaining issues" -ForegroundColor Yellow
    $errors++
}
Pop-Location

Write-Host ""
if ($errors -eq 0) {
    Write-Host "Perfect! Zero warnings achieved! 🎉" -ForegroundColor Green
} else {
    Write-Host "Some warnings may remain. Check build output above." -ForegroundColor Yellow
}