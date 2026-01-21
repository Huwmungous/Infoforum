using SfD.Mcp.Protocol.Models;
using SqliteMcpServer.Services;
using System.Text.Json;

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

    /// <summary>
    /// Returns a list of available tools supported by this server.
    /// </summary>
    public IEnumerable<object> GetAvailableTools()
    {
        return GetTools();
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger.LogInformation("Handling request: {Method}", request.Method);

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
                serverInfo = new { name = "sqlitemcpserver", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private static McpResponse HandleToolsList(McpRequest request)
    {
        object[] tools = GetTools();

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private static object[] GetTools()
    {
        return new object[]
        {
            new
            {
                name = "read_query",
                description = "Execute a SELECT query on the SQLite database",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sql = new { type = "string", description = "SQL SELECT query" },
                        parameters = new { type = "object", description = "Query parameters (optional)" }
                    },
                    required = new[] { "sql" }
                }
            },
            new
            {
                name = "write_query",
                description = "Execute an INSERT, UPDATE, or DELETE query",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sql = new { type = "string", description = "SQL INSERT/UPDATE/DELETE query" },
                        parameters = new { type = "object", description = "Query parameters (optional)" }
                    },
                    required = new[] { "sql" }
                }
            },
            new
            {
                name = "create_table",
                description = "Create a new table in the database",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table to create" },
                        columns = new { type = "object", description = "Column definitions (columnName: dataType)" }
                    },
                    required = new[] { "tableName", "columns" }
                }
            },
            new
            {
                name = "list_tables",
                description = "List all tables in the database",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new
            {
                name = "get_table_schema",
                description = "Get the schema of a table",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" }
                    },
                    required = new[] { "tableName" }
                }
            }
        };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var toolName = request.Params?.Name ?? "unknown";
        var arguments = request.Params?.Arguments ?? JsonDocument.Parse("{}").RootElement;

        try
        {
            object result = toolName switch
            {
                "read_query" => await _sqliteService.ReadQueryAsync(
                    arguments.GetProperty("sql").GetString()!,
                    arguments.TryGetProperty("parameters", out var p) ?
                        JsonSerializer.Deserialize<Dictionary<string, object>>(p.GetRawText()) : null
                ),
                "write_query" => await _sqliteService.WriteQueryAsync(
                    arguments.GetProperty("sql").GetString()!,
                    arguments.TryGetProperty("parameters", out var p2) ?
                        JsonSerializer.Deserialize<Dictionary<string, object>>(p2.GetRawText()) : null
                ),
                "create_table" => await _sqliteService.CreateTableAsync(
                    arguments.GetProperty("tableName").GetString()!,
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        arguments.GetProperty("columns").GetRawText())!
                ),
                "list_tables" => await _sqliteService.ListTablesAsync(),
                "get_table_schema" => await _sqliteService.GetTableSchemaAsync(
                    arguments.GetProperty("tableName").GetString()!
                ),
                _ => throw new Exception($"Unknown tool: {toolName}")
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call: {ToolName}", toolName);
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}