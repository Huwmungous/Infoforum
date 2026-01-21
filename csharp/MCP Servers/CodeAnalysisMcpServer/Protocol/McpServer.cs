using SfD.Mcp.Protocol.Models;
using System.Text.Json;
using CodeAnalysisMcpServer.Tools;
using SfD.Global.Logging;

namespace CodeAnalysisMcpServer.Protocol;

public class McpServer
{
    private readonly SfdLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly CodeAnalysisTools _tools;

    public McpServer(SfdLogger logger, CodeAnalysisTools tools)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        _tools = tools;
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
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handling request: {Method}", request.Method);
        }

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

    /// <summary>
    /// Handle request from JSON string and return JSON string response
    /// </summary>
    public async Task<string> HandleRequestStringAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null)
            {
                var errorResponse = new McpResponse
                {
                    Error = new McpError { Code = -32700, Message = "Parse error" }
                };
                return JsonSerializer.Serialize(errorResponse, _jsonOptions);
            }

            var response = await HandleRequestAsync(request);
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing request JSON");
            var errorResponse = new McpResponse
            {
                Error = new McpError { Code = -32700, Message = $"Parse error: {ex.Message}" }
            };
            return JsonSerializer.Serialize(errorResponse, _jsonOptions);
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
                serverInfo = new { name = "codeanalysismcpserver", version = "1.0.0" },
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
        // ✅ CODE ANALYSIS TOOLS ONLY - NO DATABASE SCHEMA TOOLS
        return
        [
            new
            {
                name = "parse_group_projects",
                description = "Parse Delphi project groups (.groupproj) and extract project information",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "parse_delphi_file",
                description = "Parse a single Delphi source file and extract structure",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the Delphi file" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "extract_sql_statements",
                description = "Extract SQL statements from a source file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "find_database_calls",
                description = "Find all database-related method calls in code",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "extract_table_references",
                description = "Extract table references from SQL code",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string", description = "SQL code to analyze" }
                    },
                    required = new[] { "code" }
                }
            },
            new
            {
                name = "analyze_procedure_calls",
                description = "Analyze stored procedure calls in code",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "find_patterns",
                description = "Find regex pattern matches in a source file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        regexPattern = new { type = "string", description = "Regular expression pattern" }
                    },
                    required = new[] { "path", "regexPattern" }
                }
            },
            new
            {
                name = "get_code_metrics",
                description = "Get code metrics (lines, complexity, etc.) for a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "extract_class_definitions",
                description = "Extract class definitions from a source file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "extract_method_signatures",
                description = "Extract method signatures from a source file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "map_data_structures",
                description = "Map data structures (records, structs) from a source file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the source file" },
                        language = new { type = "string", description = "Programming language (optional)" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "extract_runtime_assembled_sql",
                description = "Extract SQL statements that are assembled at runtime using SQL.Clear + SQL.Add patterns. Returns complete reconstructed SQL statements with method context and parameters.",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "get_sql_summary",
                description = "Get comprehensive SQL summary from all source files using unified extraction (prevents duplicates)",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "get_unified_sql_extraction",
                description = "Extract SQL using unified extractor from all sources (pas and dfm files) with no duplicates",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new
            {
                name = "get_database_object_usage",
                description = "Track all database connection, query, and command objects across .pas and .dfm files. Returns component names, types, source files, and usage locations.",
                inputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            }
            
            // ✅ NOTE: "extract_firebird_schema" tool has been REMOVED
            // That tool is now in FirebirdMcpServer where it belongs
        ];
    }

    /// <summary>
    /// Handle request from JSON string and return JSON string response
    /// </summary>
    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var toolName = request.Params?.Name ?? "unknown";
        var arguments = request.Params?.Arguments ?? JsonDocument.Parse("{}").RootElement;

        try
        {
            // ✅ CODE ANALYSIS TOOLS ONLY - NO FIREBIRD SCHEMA EXTRACTION
            object result = toolName switch
            {
                "parse_group_projects" => await _tools.ParseGroupProjectsAsync(),
                "parse_delphi_file" => await _tools.ParseDelphiFileAsync(
                    arguments.GetProperty("path").GetString()!),
                "extract_sql_statements" => await _tools.ExtractSqlStatementsAsync(
                    arguments.GetProperty("path").GetString()! ),
                "find_database_calls" => await _tools.FindDatabaseCallsAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.TryGetProperty("language", out var lang2) ? lang2.GetString() : null),
                "extract_table_references" => await _tools.ExtractTableReferencesAsync(
                    arguments.GetProperty("code").GetString()!),
                "analyze_procedure_calls" => await _tools.AnalyzeProcedureCallsAsync(
                    arguments.GetProperty("path").GetString()!),
                "find_patterns" => await _tools.FindPatternsAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.GetProperty("regexPattern").GetString()!),
                "get_code_metrics" => await _tools.GetCodeMetricsAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.TryGetProperty("language", out var lang3) ? lang3.GetString() : null),
                "extract_class_definitions" => await _tools.ExtractClassDefinitionsAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.TryGetProperty("language", out var lang4) ? lang4.GetString() : null),
                "extract_method_signatures" => await _tools.ExtractMethodSignaturesAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.TryGetProperty("language", out var lang5) ? lang5.GetString() : null),
                "map_data_structures" => await _tools.MapDataStructuresAsync(
                    arguments.GetProperty("path").GetString()!,
                    arguments.TryGetProperty("language", out var lang6) ? lang6.GetString() : null),
                "extract_runtime_assembled_sql" => await _tools.ExtractRuntimeAssembledSqlAsync(),
                "get_sql_summary" => await _tools.GetSqlSummaryAsync(),
                "get_unified_sql_extraction" => await _tools.GetUnifiedSqlExtractionAsync(),
                "get_database_object_usage" => await _tools.GetDatabaseObjectUsageAsync(),

                // ✅ NOTE: "extract_firebird_schema" case has been REMOVED
                // That tool is now handled by FirebirdMcpServer

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
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Error handling tool call: {ToolName}", toolName);
            }
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}