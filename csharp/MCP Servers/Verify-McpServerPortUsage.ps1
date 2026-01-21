# Verify and fix all MCP servers to use SfD.Global.PortResolver
$ErrorActionPreference = "Stop"

Write-Host "Verifying MCP Server Port Configuration..." -ForegroundColor Cyan
Write-Host ""

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

$issuesFound = 0
$serversFixed = 0

foreach ($serverName in $servers) {
    $programPath = ".\$serverName\Program.cs"
    
    if (!(Test-Path $programPath)) {
        Write-Host "WARNING: $serverName - Program.cs not found" -ForegroundColor Yellow
        continue
    }
    
    $content = Get-Content $programPath -Raw
    
    # Check for SfD.Global using statement
    $hasUsingStatement = $content -match "using SfD\.Global;"
    
    # Check for PortResolver.GetPort() usage
    $hasPortResolver = $content -match "PortResolver\.GetPort\(\)"
    
    # Check for Kestrel configuration
    $hasKestrelConfig = $content -match "builder\.WebHost\.ConfigureKestrel"
    
    if ($hasUsingStatement -and $hasPortResolver -and $hasKestrelConfig) {
        Write-Host "OK: $serverName - Correctly configured" -ForegroundColor Green
    }
    else {
        Write-Host "ERROR: $serverName - Issues found:" -ForegroundColor Red
        $issuesFound++
        
        if (!$hasUsingStatement) {
            Write-Host "   - Missing: using SfD.Global;" -ForegroundColor Yellow
        }
        if (!$hasPortResolver) {
            Write-Host "   - Missing: PortResolver.GetPort()" -ForegroundColor Yellow
        }
        if (!$hasKestrelConfig) {
            Write-Host "   - Missing: Kestrel configuration" -ForegroundColor Yellow
        }
        
        # Offer to fix
        Write-Host "   Would you like to fix this? (y/n): " -ForegroundColor Cyan -NoNewline
        $response = Read-Host
        
        if ($response -eq 'y') {
            # Add using statement if missing
            if (!$hasUsingStatement) {
                # Find the last using statement and add after it
                if ($content -match "(using [^;]+;)") {
                    $lastUsing = $matches[0]
                    $content = $content -replace [regex]::Escape($lastUsing), "$lastUsing`nusing SfD.Global;"
                }
            }
            
            # Fix port configuration
            if (!$hasPortResolver -or !$hasKestrelConfig) {
                # Find WebApplication.CreateBuilder
                $pattern = "(var builder = WebApplication\.CreateBuilder\(args\);)"
                if ($content -match $pattern) {
                    $portCode = 'var builder = WebApplication.CreateBuilder(args);' + "`n`n"
                    $portCode += '// CRITICAL: Use centralized port management via SfD.Global' + "`n"
                    $portCode += 'int port = PortResolver.GetPort();' + "`n"
                    $portCode += 'builder.WebHost.ConfigureKestrel(options =>' + "`n"
                    $portCode += '{' + "`n"
                    $portCode += '    options.ListenAnyIP(port);' + "`n"
                    $portCode += '});'
                    
                    $content = $content -replace $pattern, $portCode
                }
            }
            
            # Ensure blank line after usings
            $content = $content -replace '(using [^;]+;)\s*\n(var builder)', "`$1`n`n`$2"
            
            # Save the file
            Set-Content -Path $programPath -Value $content -Encoding UTF8
            
            Write-Host "   Fixed!" -ForegroundColor Green
            $serversFixed++
        }
    }
    
    Write-Host ""
}

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Summary:" -ForegroundColor White
Write-Host "  Total servers checked: $($servers.Count)" -ForegroundColor White
if ($issuesFound -gt 0) {
    Write-Host "  Issues found: $issuesFound" -ForegroundColor Yellow
} else {
    Write-Host "  Issues found: $issuesFound" -ForegroundColor Green
}
if ($serversFixed -gt 0) {
    Write-Host "  Servers fixed: $serversFixed" -ForegroundColor Green
} else {
    Write-Host "  Servers fixed: $serversFixed" -ForegroundColor White
}
Write-Host "=======================================" -ForegroundColor Cyan

if ($issuesFound -eq 0) {
    Write-Host ""
    Write-Host "All MCP servers are correctly configured!" -ForegroundColor Green
}
elseif ($serversFixed -gt 0) {
    Write-Host ""
    Write-Host "WARNING: Run 'dotnet build' to verify all changes compile correctly." -ForegroundColor Yellow
}