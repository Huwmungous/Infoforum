using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FirebirdMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();

var host = builder.Build();
await host.RunAsync();

namespace FirebirdMcpServer
{
    public class McpServer : BackgroundService
    {
        private readonly ILogger<McpServer> _logger;
        private readonly FirebirdTools _tools;

        public McpServer(ILogger<McpServer> logger)
        {
            _logger = logger;
            _tools = new FirebirdTools();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Firebird MCP Server starting...");

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

        private async Task<McpResponse> HandleRequest(McpRequest request)
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

        private Task<object> HandleInitialize()
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
                    name = "firebird-mcp-server",
                    version = "1.0.0"
                }
            });
        }

        private object HandleToolsList()
        {
            var tools = new[]
            {
                new { name = "connect_database", description = "Connect to a Firebird database" },
                new { name = "test_connection", description = "Test database connection" },
                new { name = "get_database_metadata", description = "Get database metadata" },
                new { name = "list_tables", description = "List all tables" },
                new { name = "get_table_schema", description = "Get complete table schema" },
                new { name = "get_table_columns", description = "Get table columns" },
                new { name = "get_table_indexes", description = "Get table indexes" },
                new { name = "get_table_constraints", description = "Get table constraints" },
                new { name = "list_stored_procedures", description = "List stored procedures" },
                new { name = "get_procedure_definition", description = "Get procedure source code" },
                new { name = "get_procedure_parameters", description = "Get procedure parameters" },
                new { name = "list_triggers", description = "List triggers" },
                new { name = "get_trigger_definition", description = "Get trigger source code" },
                new { name = "execute_query", description = "Execute a SQL query" },
                new { name = "get_foreign_keys", description = "Get foreign key relationships" },
                new { name = "generate_ddl", description = "Generate DDL for database objects" }
            };

            return new { tools };
        }

        private async Task<object> HandleToolCall(McpRequest request)
        {
            if (request.Params?.Arguments == null)
                throw new Exception("Missing arguments");

            var toolName = request.Params.Name;
            var args = request.Params.Arguments.Value;

            var result = toolName switch
            {
                "connect_database" => await _tools.ConnectDatabase(args),
                "test_connection" => await _tools.TestConnection(args),
                "get_database_metadata" => await _tools.GetDatabaseMetadata(args),
                "list_tables" => await _tools.ListTables(args),
                "get_table_schema" => await _tools.GetTableSchema(args),
                "get_table_columns" => await _tools.GetTableColumns(args),
                "get_table_indexes" => await _tools.GetTableIndexes(args),
                "get_table_constraints" => await _tools.GetTableConstraints(args),
                "list_stored_procedures" => await _tools.ListStoredProcedures(args),
                "get_procedure_definition" => await _tools.GetProcedureDefinition(args),
                "get_procedure_parameters" => await _tools.GetProcedureParameters(args),
                "list_triggers" => await _tools.ListTriggers(args),
                "get_trigger_definition" => await _tools.GetTriggerDefinition(args),
                "execute_query" => await _tools.ExecuteQuery(args),
                "get_foreign_keys" => await _tools.GetForeignKeys(args),
                "generate_ddl" => await _tools.GenerateDDL(args),
                _ => throw new Exception($"Unknown tool: {toolName}")
            };

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                    }
                }
            };
        }
    }
}
