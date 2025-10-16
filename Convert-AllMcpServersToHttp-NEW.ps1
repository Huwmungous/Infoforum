# Convert all MCP servers to HTTP/SSE mode with PortResolver
$ErrorActionPreference = "Stop"
Write-Host "Converting MCP Servers to HTTP/SSE mode..." -ForegroundColor Cyan

$servers = @(
    "BraveSearchMcpServer",
    "CodeAnalysisMcpServer",
    "CodeFormatterMcpServer",
    "ConfigManagementMcpServer",
    "DatabaseCompareMcpServer",
    "DocumentationMcpServer",
    "DotNetBuildMcpServer",
    "FileSystemMcpServer",
    "FileTransferMcpServer",
    "FirebirdMcpServer",
    "GitMcpServer",
    "PlaywrightMcpServer",
    "SqlGeneratorMcpServer",
    "SqliteMcpServer",
    "TestGeneratorMcpServer",
    "UiComponentConverterMcpServer"
)

foreach ($serverName in $servers) {
    $serverPath = ".\$serverName"
    $programCsPath = "$serverPath\Program.cs"
    
    if (!(Test-Path $serverPath)) {
        Write-Host "Skipping $serverName - directory not found" -ForegroundColor Yellow
        continue
    }
    
    if (!(Test-Path $programCsPath)) {
        Write-Host "Skipping $serverName - Program.cs not found" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "Converting $serverName..." -ForegroundColor Cyan
    
    # Read current Program.cs
    $content = Get-Content $programCsPath -Raw
    
    # Check if already converted
    if ($content -match "PortResolver\.GetPort\(\)") {
        Write-Host "  Already converted, skipping" -ForegroundColor Yellow
        continue
    }
    
    # Find the using statements section and add SfD.Global
    if ($content -match "(using .+?;(?:\r?\n)*)+") {
        $usingBlock = $matches[0]
        if ($usingBlock -notmatch "using SfD\.Global;") {
            $newUsingBlock = $usingBlock.TrimEnd() + "`nusing SfD.Global;`n"
            $content = $content -replace [regex]::Escape($usingBlock), $newUsingBlock
        }
    }
    
    # Find "var builder = WebApplication.CreateBuilder(args);" and add port configuration after it
    $builderPattern = "(var builder = WebApplication\.CreateBuilder\(args\);)"
    
    if ($content -match $builderPattern) {
        $portCode = @"
`$1
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});
"@
        $content = $content -replace $builderPattern, $portCode
    } else {
        Write-Host "  Could not find builder creation line - manual conversion needed" -ForegroundColor Red
        continue
    }
    
    # Remove any old port configuration if present
    $content = $content -replace 'var port = Environment\.GetEnvironmentVariable\("MCP_PORT"\)[^;]+;', ''
    $content = $content -replace 'builder\.WebHost\.UseUrls\([^\)]+\);', ''
    
    # Write back to file
    [System.IO.File]::WriteAllText($programCsPath, $content)
    
    Write-Host "  ✓ Converted $serverName" -ForegroundColor Green
}

Write-Host ""
Write-Host "Conversion complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Add HandleRequestStringAsync method to each Protocol/McpServer.cs" -ForegroundColor White
Write-Host "  2. Build all projects: dotnet build" -ForegroundColor White
Write-Host "  3. Test locally to verify HTTP endpoints work" -ForegroundColor White
Write-Host "  4. Deploy to Linux using deploy-all-MCPServers.sh" -ForegroundColor White
