using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CodeAnalysisMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();

var host = builder.Build();
await host.RunAsync();

namespace CodeAnalysisMcpServer
{
    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private readonly ILogger<McpServer> _logger = logger;
        private readonly CodeAnalysisTools _tools = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Code Analysis MCP Server starting...");

            try
            {
                using var reader = new StreamReader(Console.OpenStandardInput());
                using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

                while (!stoppingToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(stoppingToken);
                    if (line == null) break;

                    try
                    {
                        var request = JsonSerializer.Deserialize<McpRequest>(line);
                        if (request == null) continue;

                        var response = await HandleRequest(request);
                        var responseJson = JsonSerializer.Serialize(response);
                        await writer.WriteLineAsync(responseJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing request");
                        var errorResponse = new McpResponse
                        {
                            Jsonrpc = "2.0",
                            Id = null,
                            Error = new McpError
                            {
                                Code = -32603,
                                Message = ex.Message
                            }
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in MCP server");
            }
        }

        private static async Task<McpResponse> HandleRequest(McpRequest request)
        {
            try
            {
                object? result = request.Method switch
                {
                    "initialize" => await HandleInitialize(),
                    "tools/list" => HandleToolsList(),
                    "tools/call" => await HandleToolCall(request),
                    _ => throw new Exception($"Unknown method: {request.Method}")
                };

                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = ex.Message
                    }
                };
            }
        }

        private static Task<object> HandleInitialize()
        {
            return Task.FromResult<object>(new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "codeanalysis-mcp-server",
                    version = "1.0.0"
                }
            });
        }

        private static object HandleToolsList()
        {
            var tools = new[]
            {
                new { name = "parse_delphi_file", description = "Parse a Delphi source file" },
                new { name = "extract_sql_statements", description = "Extract SQL statements from code" },
                new { name = "find_database_calls", description = "Find database component calls" },
                new { name = "extract_table_references", description = "Extract table names from SQL" },
                new { name = "analyze_procedure_calls", description = "Find stored procedure calls" },
                new { name = "find_patterns", description = "Find regex patterns in code" },
                new { name = "get_code_metrics", description = "Get code statistics" },
                new { name = "extract_class_definitions", description = "Extract class definitions" },
                new { name = "extract_method_signatures", description = "Extract method signatures" },
                new { name = "map_data_structures", description = "Extract data structures and records" }
            };

            return new { tools };
        }

        private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() { WriteIndented = true };

        private static async Task<object> HandleToolCall(McpRequest request)
        {
            if (request.Params?.Arguments == null)
                throw new Exception("Missing arguments");

            var toolName = request.Params.Name;
            var args = request.Params.Arguments.Value;

            var result = toolName switch
            {
                "parse_delphi_file" => await CodeAnalysisTools.ParseDelphiFile(args),
                "extract_sql_statements" => await CodeAnalysisTools.ExtractSqlStatements(args),
                "find_database_calls" => await CodeAnalysisTools.FindDatabaseCalls(args),
                "extract_table_references" => await CodeAnalysisTools.ExtractTableReferences(args), // Fixed here
                "analyze_procedure_calls" => await CodeAnalysisTools.AnalyzeProcedureCalls(args),
                "find_patterns" => await CodeAnalysisTools.FindPatterns(args),
                "get_code_metrics" => await CodeAnalysisTools.GetCodeMetrics(args), // Fixed here
                "extract_class_definitions" => await CodeAnalysisTools.ExtractClassDefinitions(args),
                "extract_method_signatures" => await CodeAnalysisTools.ExtractMethodSignatures(args),
                "map_data_structures" => await CodeAnalysisTools.MapDataStructures(args),
                _ => throw new Exception($"Unknown tool: {toolName}")
            };


            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, CachedJsonSerializerOptions)
                    }
                }
            };
        }
    }
}
