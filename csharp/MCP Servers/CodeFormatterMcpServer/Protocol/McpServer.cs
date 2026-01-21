using CodeFormatterMcpServer.Services;
using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace CodeFormatterMcpServer.Protocol;

public class McpServer(CodeFormatterService formatterService, ILogger<McpServer> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

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
            logger.LogError(ex, "JSON parsing error");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32700, Message = $"Parse error: {ex.Message}" }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling request");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32603, Message = $"Internal error: {ex.Message}" }
            }, _jsonOptions);
        }
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        logger.LogInformation("Handling request: {Method}", request.Method);

        await Task.CompletedTask;

        try
        {
            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => HandleToolCall(request),
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError { Code = -32601, Message = "Method not found" }
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling request");
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
                serverInfo = new { name = "code-formatter-mcp", version = "1.0.0" },
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
        return [
            new
            {
                name = "format_csharp",
                description = "Format C# code using Roslyn with proper indentation and style.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new
                        {
                            type = "string",
                            description = "C# code to format"
                        }
                    },
                    required = new[] { "code" }
                }
            },
            new
            {
                name = "format_delphi",
                description = "Format Delphi/Pascal code with proper indentation.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new
                        {
                            type = "string",
                            description = "Delphi/Pascal code to format"
                        }
                    },
                    required = new[] { "code" }
                }
            },
            new
            {
                name = "format_sql",
                description = "Format SQL code with proper indentation and keyword capitalization.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new
                        {
                            type = "string",
                            description = "SQL code to format"
                        }
                    },
                    required = new[] { "code" }
                }
            },
            new
            {
                name = "format_file",
                description = "Format a code file (auto-detect type from extension: .cs, .pas, .dpr, .sql).",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to file to format"
                        }
                    },
                    required = new[] { "filePath" }
                }
            }
        ];
    }

    private McpResponse HandleToolCall(McpRequest request)
    {
        var arguments = request.Params?.Arguments;

        try
        {
            logger.LogInformation("Executing tool: {ToolName}", request.Params?.Name);

            if (arguments == null)
            {
                throw new ArgumentException("Missing arguments");
            }

            var result = request.Params?.Name switch
            {
                "format_csharp" => formatterService.FormatCSharp(
                    GetRequiredArg(arguments, "code")),

                "format_delphi" => formatterService.FormatDelphi(
                    GetRequiredArg(arguments, "code")),

                "format_sql" => formatterService.FormatSql(
                    GetRequiredArg(arguments, "code")),

                "format_file" => formatterService.FormatFile(
                    GetRequiredArg(arguments, "filePath")),

                _ => throw new Exception($"Unknown tool: {request.Params?.Name}")
            };

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
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32602, Message = $"Invalid params: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing tool");
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
}