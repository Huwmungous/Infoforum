using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace SqlGeneratorMcpServer.Protocol;

public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(ILogger<McpServer> logger)
    {
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
                serverInfo = new { name = "sqlgeneratormcpserver", version = "1.0.0" },
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
                name = "generate_select",
                description = "Generate a SELECT SQL statement",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" },
                        columns = new { type = "array", items = new { type = "string" }, description = "Columns to select (default: *)" },
                        whereClause = new { type = "string", description = "WHERE clause (optional)" }
                    },
                    required = new[] { "tableName" }
                }
            },
            new
            {
                name = "generate_insert",
                description = "Generate an INSERT SQL statement",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" },
                        columns = new { type = "array", items = new { type = "string" }, description = "Columns to insert" }
                    },
                    required = new[] { "tableName", "columns" }
                }
            },
            new
            {
                name = "generate_update",
                description = "Generate an UPDATE SQL statement",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" },
                        columns = new { type = "array", items = new { type = "string" }, description = "Columns to update" },
                        whereClause = new { type = "string", description = "WHERE clause" }
                    },
                    required = new[] { "tableName", "columns", "whereClause" }
                }
            },
            new
            {
                name = "generate_delete",
                description = "Generate a DELETE SQL statement",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" },
                        whereClause = new { type = "string", description = "WHERE clause" }
                    },
                    required = new[] { "tableName", "whereClause" }
                }
            },
            new
            {
                name = "translate_sql",
                description = "Translate SQL from one dialect to another",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceSql = new { type = "string", description = "Source SQL statement" },
                        sourceDialect = new { type = "string", description = "Source SQL dialect (e.g., firebird, sqlserver)" },
                        targetDialect = new { type = "string", description = "Target SQL dialect (e.g., firebird, sqlserver)" }
                    },
                    required = new[] { "sourceSql", "sourceDialect", "targetDialect" }
                }
            },
            new
            {
                name = "parameterize_sql",
                description = "Convert string literals in SQL to parameters",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sql = new { type = "string", description = "SQL statement to parameterize" }
                    },
                    required = new[] { "sql" }
                }
            },
            new
            {
                name = "generate_stored_proc_call",
                description = "Generate a stored procedure call statement",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        procedureName = new { type = "string", description = "Name of the stored procedure" },
                        parameters = new { type = "array", items = new { type = "string" }, description = "Parameter names" }
                    },
                    required = new[] { "procedureName" }
                }
            },
            new
            {
                name = "generate_csharp_entity",
                description = "Generate a C# entity class from table schema",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        tableName = new { type = "string", description = "Name of the table" },
                        columns = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Column name" },
                                    type = new { type = "string", description = "SQL data type" },
                                    isNullable = new { type = "boolean", description = "Whether column is nullable" }
                                }
                            }
                        }
                    },
                    required = new[] { "tableName", "columns" }
                }
            },
            new
            {
                name = "generate_repository_interface",
                description = "Generate a repository interface for an entity",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        entityName = new { type = "string", description = "Name of the entity" },
                        operations = new { type = "array", items = new { type = "string" }, description = "Operations to include (GetAll, GetById, Add, Update, Delete)" }
                    },
                    required = new[] { "entityName" }
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
                "generate_select" => await SqlGeneratorTools.GenerateSelect(arguments),
                "generate_insert" => await SqlGeneratorTools.GenerateInsert(arguments),
                "generate_update" => await SqlGeneratorTools.GenerateUpdate(arguments),
                "generate_delete" => await SqlGeneratorTools.GenerateDelete(arguments),
                "translate_sql" => await SqlGeneratorTools.TranslateSql(arguments),
                "parameterize_sql" => await SqlGeneratorTools.ParameterizeSql(arguments),
                "generate_stored_proc_call" => await SqlGeneratorTools.GenerateStoredProcCall(arguments),
                "generate_csharp_entity" => await SqlGeneratorTools.GenerateCSharpEntity(arguments),
                "generate_repository_interface" => await SqlGeneratorTools.GenerateRepositoryInterface(arguments),
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