# Add HandleRequestStringAsync method to all MCP Server Protocol classes
$ErrorActionPreference = "Stop"
Write-Host "Adding HTTP handler methods to MCP Servers..." -ForegroundColor Cyan

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

$httpHandlerMethod = @"
    public async Task<string> HandleRequestStringAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null)
            {
                return JsonSerializer.Serialize(new McpResponse
                {
                    Id = 0,
                    Error = new McpError { Code = -32700, Message = "Parse error" }
                }, _jsonOptions);
            }

            McpResponse response;
            if (request.Method == "tools/call")
            {
                response = await HandleToolCallAsync(request);
            }
            else
            {
                response = await HandleRequestAsync(request);
            }

            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request");
            return JsonSerializer.Serialize(new McpResponse
            {
                Id = 0,
                Error = new McpError { Code = -32603, Message = ex.Message }
            }, _jsonOptions);
        }
    }
"@

foreach ($serverName in $servers) {
    $mcpServerPath = ".\$serverName\Protocol\McpServer.cs"
    
    if (!(Test-Path $mcpServerPath)) {
        Write-Host "Skipping $serverName - McpServer.cs not found" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "Processing $serverName..." -ForegroundColor Cyan
    
    # Read current McpServer.cs
    $content = Get-Content $mcpServerPath -Raw
    
    # Check if already has the method
    if ($content -match "HandleRequestStringAsync") {
        Write-Host "  Already has HTTP handler, skipping" -ForegroundColor Yellow
        continue
    }
    
    # Find the last closing brace of the class (before the final namespace closing brace)
    # We want to add the method before the last }
    $lastBraceIndex = $content.LastIndexOf('}')
    
    if ($lastBraceIndex -gt 0) {
        # Insert the method before the last brace
        $beforeBrace = $content.Substring(0, $lastBraceIndex)
        $afterBrace = $content.Substring($lastBraceIndex)
        
        $content = $beforeBrace + $httpHandlerMethod + "`n" + $afterBrace
        
        # Write back to file
        [System.IO.File]::WriteAllText($mcpServerPath, $content)
        
        Write-Host "  ✓ Added HTTP handler to $serverName" -ForegroundColor Green
    } else {
        Write-Host "  Could not find insertion point - manual edit needed" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "HTTP handlers added!" -ForegroundColor Green
Write-Host ""
Write-Host "Next step: Build and test" -ForegroundColor Yellow
Write-Host "  dotnet build" -ForegroundColor White
