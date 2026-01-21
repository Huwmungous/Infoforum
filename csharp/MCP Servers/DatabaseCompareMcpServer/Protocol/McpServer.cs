using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace DatabaseCompareMcpServer.Protocol;

public class McpServer(ILogger<McpServer> logger)
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
                serverInfo = new { name = "database-compare-mcp", version = "1.0.0" },
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
                name = "compare_schemas",
                description = "Compare schemas between two Firebird databases and identify differences.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceConnectionString = new
                        {
                            type = "string",
                            description = "Source database connection string"
                        },
                        targetConnectionString = new
                        {
                            type = "string",
                            description = "Target database connection string"
                        }
                    },
                    required = new[] { "sourceConnectionString", "targetConnectionString" }
                }
            },
            new
            {
                name = "generate_migration_script",
                description = "Generate SQL migration script to sync schemas between two databases.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceConnectionString = new
                        {
                            type = "string",
                            description = "Source database connection string"
                        },
                        targetConnectionString = new
                        {
                            type = "string",
                            description = "Target database connection string"
                        }
                    },
                    required = new[] { "sourceConnectionString", "targetConnectionString" }
                }
            },
            new
            {
                name = "validate_foreign_keys",
                description = "Validate all foreign key constraints in a Firebird database.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        connectionString = new
                        {
                            type = "string",
                            description = "Database connection string"
                        }
                    },
                    required = new[] { "connectionString" }
                }
            },
            new
            {
                name = "compare_table_data",
                description = "Compare row counts between the same table in two databases.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceConnectionString = new
                        {
                            type = "string",
                            description = "Source database connection string"
                        },
                        targetConnectionString = new
                        {
                            type = "string",
                            description = "Target database connection string"
                        },
                        tableName = new
                        {
                            type = "string",
                            description = "Table name to compare"
                        }
                    },
                    required = new[] { "sourceConnectionString", "targetConnectionString", "tableName" }
                }
            },
            new
            {
                name = "find_orphaned_records",
                description = "Find records with foreign key values that don't exist in the referenced table.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        connectionString = new
                        {
                            type = "string",
                            description = "Database connection string"
                        },
                        tableName = new
                        {
                            type = "string",
                            description = "Table to check for orphaned records"
                        },
                        foreignKeyColumn = new
                        {
                            type = "string",
                            description = "Foreign key column name"
                        },
                        referencedTable = new
                        {
                            type = "string",
                            description = "Referenced table name"
                        },
                        referencedColumn = new
                        {
                            type = "string",
                            description = "Referenced column name"
                        }
                    },
                    required = new[] { "connectionString", "tableName", "foreignKeyColumn", "referencedTable", "referencedColumn" }
                }
            }
        ];
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var arguments = request.Params?.Arguments;

        try
        {
            logger.LogInformation("Executing tool: {ToolName}", request.Params?.Name);

            if (arguments == null)
            {
                throw new ArgumentException("Missing arguments");
            }

            object result = request.Params?.Name switch
            {
                "compare_schemas" => await DatabaseCompareTools.CompareSchemas(arguments.Value),
                "generate_migration_script" => await DatabaseCompareTools.GenerateMigrationScript(arguments.Value),
                "validate_foreign_keys" => await DatabaseCompareTools.ValidateForeignKeys(arguments.Value),
                "compare_table_data" => await DatabaseCompareTools.CompareTableData(arguments.Value),
                "find_orphaned_records" => await DatabaseCompareTools.FindOrphanedRecords(arguments.Value),
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
}