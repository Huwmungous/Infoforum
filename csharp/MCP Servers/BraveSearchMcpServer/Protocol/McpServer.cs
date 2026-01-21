using BraveSearchMcpServer.Services;
using SfD.Mcp.Protocol.Models;
using System.Text.Json;

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

    /// <summary>
    /// Returns a list of available tools supported by this server.
    /// </summary>
    public IEnumerable<object> GetAvailableTools()
    {
        return GetTools();
    }

    public async Task<string> HandleRequestStringAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null)
            {
                return JsonSerializer.Serialize(new McpResponse
                {
                    Error = new McpError { Code = -32700, Message = "Parse error" }
                }, _jsonOptions);
            }

            var response = await HandleRequestAsync(request);
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32700, Message = $"Parse error: {ex.Message}" }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling request");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32603, Message = $"Internal error: {ex.Message}" }
            }, _jsonOptions);
        }
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger.LogInformation("Handling request: {Method}", request.Method);

        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32603, Message = ex.Message }
            };
        }
    }

    private static McpResponse HandleInitialize(McpRequest request)
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

    private static McpResponse HandleToolsList(McpRequest request)
    {
        object[] tools = GetTools();

        return new McpResponse
        {
            Id = request.Id,
            Result = new { tools }
        };
    }

    private static object[] GetTools()
    {
        return new[]
        {
            new
            {
                name = "brave_search",
                description = "Search the web using Brave Search API. Returns web search results with titles, URLs, and descriptions.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Search query string"
                        },
                        count = new
                        {
                            type = "integer",
                            description = "Number of results to return (default: 10, max: 20)",
                            @default = 10
                        }
                    },
                    required = new[] { "query" }
                }
            }
        };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var toolName = request.Params?.Name ?? "unknown";
        var arguments = request.Params?.Arguments;

        try
        {
            _logger.LogInformation("Executing tool: {ToolName}", toolName);

            if (arguments == null)
            {
                throw new ArgumentException("Missing arguments");
            }

            if (toolName == "brave_search")
            {
                var query = GetRequiredArg(arguments, "query");
                var count = GetOptionalIntArg(arguments, "count") ?? 10;

                // Limit count to reasonable maximum
                count = Math.Min(count, 20);

                var result = await _searchService.SearchAsync(query, count);

                return new McpResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = JsonSerializer.Serialize(result, _jsonOptions)
                            }
                        }
                    }
                };
            }

            throw new Exception($"Unknown tool: {toolName}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid arguments");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32602, Message = $"Invalid params: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }

    private static string GetRequiredArg(JsonElement? arguments, string name)
    {
        if (arguments == null || !arguments.Value.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Missing required argument: {name}");
        }

        var stringValue = value.GetString();
        if (string.IsNullOrEmpty(stringValue))
        {
            throw new ArgumentException($"Argument '{name}' cannot be empty");
        }

        return stringValue;
    }

    private static int? GetOptionalIntArg(JsonElement? arguments, string name)
    {
        if (arguments == null || !arguments.Value.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
    }
}
