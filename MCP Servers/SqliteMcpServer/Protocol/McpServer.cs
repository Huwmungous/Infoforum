using SqliteMcpServer.Models;
using SqliteMcpServer.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SqliteMcpServer.Protocol;

public class McpServer
{
    private readonly SqliteService _sqliteService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(SqliteService sqliteService, ILogger<McpServer> logger)
    {
        _sqliteService = sqliteService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SQLite MCP Server starting");
        
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
                serverInfo = new { name = "sqlite-mcp", version = "1.0.0" },
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
                Name = "read_query",
                Description = "Execute SELECT query",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sql = new { type = "string" },
                        parameters = new { type = "object" }
                    },
                    required = new[] { "sql" }
                }
            },
            new ToolInfo
            {
                Name = "write_query",
                Description = "Execute INSERT UPDATE DELETE",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sql = new { type = "string" },
                        parameters = new { type = "object" }
                    },
                    required = new[] { "sql" }
                }
            },
            new ToolInfo
            {
                Name = "create_table",
                Description = "Create a new table",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string" },
                        columns = new { type = "object" }
                    },
                    required = new[] { "tableName", "columns" }
                }
            },
            new ToolInfo
            {
                Name = "list_tables",
                Description = "List all tables",
                InputSchema = new { type = "object" }
            },
            new ToolInfo
            {
                Name = "get_table_schema",
                Description = "Get table schema",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string" }
                    },
                    required = new[] { "tableName" }
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
                "read_query" => await _sqliteService.ReadQueryAsync(
                    arguments.GetProperty("sql").GetString()!,
                    arguments.TryGetProperty("parameters", out var p) ? 
                        JsonSerializer.Deserialize<Dictionary<string, object>>(p) : null
                ),
                "write_query" => await _sqliteService.WriteQueryAsync(
                    arguments.GetProperty("sql").GetString()!,
                    arguments.TryGetProperty("parameters", out var p2) ? 
                        JsonSerializer.Deserialize<Dictionary<string, object>>(p2) : null
                ),
                "create_table" => await _sqliteService.CreateTableAsync(
                    arguments.GetProperty("tableName").GetString()!,
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        arguments.GetProperty("columns"))!
                ),
                "list_tables" => await _sqliteService.ListTablesAsync(),
                "get_table_schema" => await _sqliteService.GetTableSchemaAsync(
                    arguments.GetProperty("tableName").GetString()!
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