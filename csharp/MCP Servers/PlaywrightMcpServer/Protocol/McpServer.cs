 
using PlaywrightMcpServer.Models;
using PlaywrightMcpServer.Services;
using SfD.Mcp.Protocol.Models;
using System.Text.Json; 

namespace PlaywrightMcpServer.Protocol;

public class McpServer
{
    private readonly PlaywrightService _playwrightService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(PlaywrightService playwrightService, ILogger<McpServer> logger)
    {
        _playwrightService = playwrightService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Playwright MCP Server starting");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) break;

                try
                {
                    var request = JsonSerializer.Deserialize<PlaywrightMcpRequest>(line, _jsonOptions);
                    if (request == null) continue;

                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    
                    await Console.Out.WriteLineAsync(responseJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request error");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested");
        }
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            _ => new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32601, Message = "Method not found" }
            }
        };
    }

    private static McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "playwright-mcp", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private static McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new[]
        {
            new ToolInfo
            {
                Name = "playwright_navigate",
                Description = "Navigate to a URL",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new { type = "string" },
                        timeout = new { type = "integer" }
                    },
                    required = new[] { "url" }
                }
            },
            new ToolInfo
            {
                Name = "playwright_screenshot",
                Description = "Take a screenshot",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string" },
                        fullPage = new { type = "boolean" }
                    }
                }
            },
            new ToolInfo
            {
                Name = "playwright_evaluate",
                Description = "Execute JavaScript",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        script = new { type = "string" }
                    },
                    required = new[] { "script" }
                }
            }
        };

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var paramsElement = (JsonElement)request.Params!;
        var toolName = paramsElement.GetProperty("name").GetString()!;
        var arguments = paramsElement.GetProperty("arguments");

        try
        {
            object result = toolName switch
            {
                "playwright_navigate" => await _playwrightService.NavigateAsync(
                    arguments.GetProperty("url").GetString()!,
                    arguments.TryGetProperty("timeout", out var t) ? t.GetInt32() : null
                ),
                "playwright_screenshot" => await _playwrightService.ScreenshotAsync(
                    arguments.TryGetProperty("path", out var p) ? p.GetString() : null,
                    arguments.TryGetProperty("fullPage", out var fp) && fp.GetBoolean()
                ),
                "playwright_evaluate" => await _playwrightService.EvaluateAsync(
                    arguments.GetProperty("script").GetString()!
                ),
                _ => throw new InvalidOperationException("Unknown tool")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(result, _jsonOptions) }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}