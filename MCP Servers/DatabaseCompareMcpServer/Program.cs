
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DatabaseCompareMcpServer;



    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHostedService<McpServer>();
    await builder.Build().RunAsync();


namespace DatabaseCompareMcpServer
{
    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Database Compare MCP Server starting...");
            try {
                using var reader = new StreamReader(Console.OpenStandardInput());
                using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                while(!stoppingToken.IsCancellationRequested) {
                    var line = await reader.ReadLineAsync(stoppingToken);
                    if(line == null) break;
                    try {
                        var request = JsonSerializer.Deserialize<McpRequest>(line);
                        if(request == null) continue;
                        var response = await HandleRequest(request);
                        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                    } catch(Exception ex) {
                        logger.LogError(ex, "Error");
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new McpResponse { Jsonrpc = "2.0", Error = new McpError { Code = -32603, Message = ex.Message } }));
                    }
                }
            } catch(Exception ex) { logger.LogError(ex, "Fatal"); }
        }
        private static async Task<McpResponse> HandleRequest(McpRequest request) {
            try {
                object? result = request.Method switch {
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "dbcompare-mcp-server", version = "1.0.0" } },
                    "tools/list" => new { tools = new[] {
                    new { name = "compare_schemas", description = "Compare database schemas" },
                    new { name = "generate_migration_script", description = "Generate migration script" },
                    new { name = "validate_foreign_keys", description = "Validate foreign key constraints" },
                    new { name = "compare_table_data", description = "Compare row counts between tables" },
                    new { name = "find_orphaned_records", description = "Find orphaned records" }
                } },
                    "tools/call" => await HandleToolCall(request),
                    _ => throw new Exception($"Unknown method: {request.Method}")
                };
                return new McpResponse { Jsonrpc = "2.0", Id = request.Id, Result = result };
            } catch(Exception ex) {
                return new McpResponse { Jsonrpc = "2.0", Id = request.Id, Error = new McpError { Code = -32603, Message = ex.Message } };
            }
        }
        private static async Task<object> HandleToolCall(McpRequest request) {
            if(request.Params?.Arguments == null) throw new Exception("Missing arguments");
            var args = request.Params.Arguments.Value;
            var result = request.Params.Name switch {
                "compare_schemas" => await DatabaseCompareTools.CompareSchemas(args),
                "generate_migration_script" => await DatabaseCompareTools.GenerateMigrationScript(args),
                "validate_foreign_keys" => await DatabaseCompareTools.ValidateForeignKeys(args),
                "compare_table_data" => await DatabaseCompareTools.CompareTableData(args),
                "find_orphaned_records" => await DatabaseCompareTools.FindOrphanedRecords(args),
                _ => throw new Exception($"Unknown tool: {request.Params.Name}")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, Opts) } } };
        }
    }

}
