using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocumentationMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();
await builder.Build().RunAsync();

namespace DocumentationMcpServer
{

    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Documentation MCP Server starting...");
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
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "docs-mcp-server", version = "1.0.0" } },
                    "tools/list" => new
                    {
                        tools = new[] {
                    new { name = "generate_readme", description = "Generate README.md" },
                    new { name = "generate_api_docs", description = "Generate API documentation" },
                    new { name = "create_migration_report", description = "Create migration progress report" },
                    new { name = "generate_changelog", description = "Generate CHANGELOG.md" },
                    new { name = "generate_xml_comments", description = "Generate XML documentation comments" },
                    new { name = "generate_class_diagram", description = "Generate class diagram (Mermaid)" }
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
                "generate_readme" => await DocumentationTools.GenerateReadme(args),
                "generate_api_docs" => await DocumentationTools.GenerateApiDocs(args),
                "create_migration_report" => await DocumentationTools.CreateMigrationReport(args),
                "generate_changelog" => await DocumentationTools.GenerateChangelog(args),
                "generate_xml_comments" => await DocumentationTools.GenerateXmlComments(args),
                "generate_class_diagram" => await DocumentationTools.GenerateClassDiagram(args),
                _ => throw new Exception($"Unknown tool: {request.Params.Name}")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, Opts) } } };
        }
    }

}