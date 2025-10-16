using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqlGeneratorMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();
await builder.Build().RunAsync();

namespace SqlGeneratorMcpServer
{

    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("SQL Generator MCP Server starting...");
            try
            {
                using var reader = new StreamReader(Console.OpenStandardInput());
                using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                while(!stoppingToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(stoppingToken);
                    if(line == null) break;
                    try
                    {
                        var request = JsonSerializer.Deserialize<McpRequest>(line);
                        if(request == null) continue;
                        var response = await HandleRequest(request);
                        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
                    }
                    catch(Exception ex)
                    {
                        logger.LogError(ex, "Error");
                        await writer.WriteLineAsync(JsonSerializer.Serialize(new McpResponse { Jsonrpc = "2.0", Error = new McpError { Code = -32603, Message = ex.Message } }));
                    }
                }
            }
            catch(Exception ex) { logger.LogError(ex, "Fatal"); }
        }
        private static async Task<McpResponse> HandleRequest(McpRequest request)
        {
            try
            {
                object? result = request.Method switch
                {
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "sqlgen-mcp-server", version = "1.0.0" } },
                    "tools/list" => new
                    {
                        tools = new[] {
                    new { name = "generate_select", description = "Generate SELECT query" },
                    new { name = "generate_insert", description = "Generate INSERT query" },
                    new { name = "generate_update", description = "Generate UPDATE query" },
                    new { name = "generate_delete", description = "Generate DELETE query" },
                    new { name = "translate_sql", description = "Translate SQL between dialects" },
                    new { name = "parameterize_sql", description = "Convert to parameterized query" },
                    new { name = "generate_stored_proc_call", description = "Generate stored proc call" },
                    new { name = "generate_csharp_entity", description = "Generate C# entity from table" },
                    new { name = "generate_repository_interface", description = "Generate repository interface" }
                }
                    },
                    "tools/call" => await HandleToolCall(request),
                    _ => throw new Exception($"Unknown method: {request.Method}")
                };
                return new McpResponse { Jsonrpc = "2.0", Id = request.Id, Result = result };
            }
            catch(Exception ex)
            {
                return new McpResponse { Jsonrpc = "2.0", Id = request.Id, Error = new McpError { Code = -32603, Message = ex.Message } };
            }
        }
        private static async Task<object> HandleToolCall(McpRequest request)
        {
            if(request.Params?.Arguments == null) throw new Exception("Missing arguments");
            var args = request.Params.Arguments.Value;
            var result = request.Params.Name switch
            {
                "generate_select" => await SqlGeneratorTools.GenerateSelect(args),
                "generate_insert" => await SqlGeneratorTools.GenerateInsert(args),
                "generate_update" => await SqlGeneratorTools.GenerateUpdate(args),
                "generate_delete" => await SqlGeneratorTools.GenerateDelete(args),
                "translate_sql" => await SqlGeneratorTools.TranslateSql(args),
                "parameterize_sql" => await SqlGeneratorTools.ParameterizeSql(args),
                "generate_stored_proc_call" => await SqlGeneratorTools.GenerateStoredProcCall(args),
                "generate_csharp_entity" => await SqlGeneratorTools.GenerateCSharpEntity(args),
                "generate_repository_interface" => await SqlGeneratorTools.GenerateRepositoryInterface(args),
                _ => throw new Exception($"Unknown tool: {request.Params.Name}")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, Opts) } } };
        }
    }

}