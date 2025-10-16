using BraveSearchMcpServer.Models;
using BraveSearchMcpServer.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BraveSearchMcpServer.Protocol;

public class McpServer
{
    private readonly BraveSearchService _searchService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(BraveSearchService searchService, ILogger<McpServer> logger)
    {
        _searchService = searchService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Brave Search MCP Server starting");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) break;

                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
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

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
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

    private McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "brave-search-mcp", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new[]
        {
            new ToolInfo
            {
                Name = "brave_search",
                Description = "Search the web for documentation",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string" },
                        count = new { type = "integer" }
                    },
                    required = new[] { "query" }
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
            if (toolName == "brave_search")
            {
                var query = arguments.GetProperty("query").GetString()!;
                var count = arguments.TryGetProperty("count", out var c) ? c.GetInt32() : 10;
                
                var result = await _searchService.SearchAsync(query, count);

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

            throw new InvalidOperationException("Unknown tool");
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