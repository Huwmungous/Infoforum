using CodeFormatterMcpServer.Models;
using CodeFormatterMcpServer.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CodeFormatterMcpServer.Protocol;

public class McpServer
{
    private readonly CodeFormatterService _formatterService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(CodeFormatterService formatterService, ILogger<McpServer> logger)
    {
        _formatterService = formatterService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Code Formatter MCP Server starting");
        
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
        await Task.CompletedTask;
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

    private McpResponse HandleInitialize(McpRequest request)
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

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new[]
        {
            new ToolInfo
            {
                Name = "format_csharp",
                Description = "Format C# code using Roslyn",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" }
                    },
                    required = new[] { "code" }
                }
            },
            new ToolInfo
            {
                Name = "format_delphi",
                Description = "Format Delphi/Pascal code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" }
                    },
                    required = new[] { "code" }
                }
            },
            new ToolInfo
            {
                Name = "format_sql",
                Description = "Format SQL code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" }
                    },
                    required = new[] { "code" }
                }
            },
            new ToolInfo
            {
                Name = "format_file",
                Description = "Format a code file (auto-detect type)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string" }
                    },
                    required = new[] { "filePath" }
                }
            }
        };

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private McpResponse HandleToolCall(McpRequest request)
    {
        var paramsElement = (JsonElement)request.Params!;
        var toolName = paramsElement.GetProperty("name").GetString()!;
        var arguments = paramsElement.GetProperty("arguments");

        try
        {
            FormatResult result = toolName switch
            {
                "format_csharp" => _formatterService.FormatCSharp(
                    arguments.GetProperty("code").GetString()!
                ),
                "format_delphi" => _formatterService.FormatDelphi(
                    arguments.GetProperty("code").GetString()!
                ),
                "format_sql" => _formatterService.FormatSql(
                    arguments.GetProperty("code").GetString()!
                ),
                "format_file" => _formatterService.FormatFile(
                    arguments.GetProperty("filePath").GetString()!
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