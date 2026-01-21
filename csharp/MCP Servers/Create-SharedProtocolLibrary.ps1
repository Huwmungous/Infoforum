# Create shared MCP Protocol library
$ErrorActionPreference = "Stop"

Write-Host "Creating Shared MCP Protocol Library..." -ForegroundColor Cyan

$projectName = "SfD.Mcp.Protocol"
$projectPath = ".\$projectName"

# Create project
if (Test-Path $projectPath) {
    Write-Host "Project already exists, skipping creation..." -ForegroundColor Yellow
} else {
    dotnet new classlib -n $projectName -f net9.0
    Write-Host "Created project: $projectName" -ForegroundColor Green
}

# Create Models directory
New-Item -Path "$projectPath\Models" -ItemType Directory -Force | Out-Null

# Create McpRequest.cs
$mcpRequest = @'
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SfD.Mcp.Protocol.Models;

public class McpRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";
    
    [JsonPropertyName("params")]
    public McpParams? Params { get; set; }
}

public class McpParams
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; set; }
}
'@
Set-Content -Path "$projectPath\Models\McpRequest.cs" -Value $mcpRequest -Encoding UTF8

# Create McpResponse.cs
$mcpResponse = @'
using System.Text.Json.Serialization;

namespace SfD.Mcp.Protocol.Models;

public class McpResponse
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")]
    public object? Id { get; set; }
    
    [JsonPropertyName("result")]
    public object? Result { get; set; }
    
    [JsonPropertyName("error")]
    public McpError? Error { get; set; }
}

public class McpError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    
    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
'@
Set-Content -Path "$projectPath\Models\McpResponse.cs" -Value $mcpResponse -Encoding UTF8

# Create ToolInfo.cs
$toolInfo = @'
using System.Text.Json.Serialization;

namespace SfD.Mcp.Protocol.Models;

public class ToolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; set; }
}
'@
Set-Content -Path "$projectPath\Models\ToolInfo.cs" -Value $toolInfo -Encoding UTF8

# Delete default Class1.cs
Remove-Item "$projectPath\Class1.cs" -ErrorAction SilentlyContinue

# Build the library
Write-Host "Building shared protocol library..." -ForegroundColor Cyan
Push-Location $projectPath
dotnet build -c Release | Out-Null
Pop-Location

Write-Host ""
Write-Host "Shared Protocol Library Created Successfully!" -ForegroundColor Green
Write-Host "Location: $projectPath" -ForegroundColor White
Write-Host ""
Write-Host "To use in your MCP servers:" -ForegroundColor Yellow
Write-Host "  dotnet add reference ..\SfD.Mcp.Protocol\SfD.Mcp.Protocol.csproj" -ForegroundColor White