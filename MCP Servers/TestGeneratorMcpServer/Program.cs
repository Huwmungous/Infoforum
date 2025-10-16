using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestGeneratorMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();
await builder.Build().RunAsync();

namespace TestGeneratorMcpServer
{

    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Test Generator MCP Server starting...");
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
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "testgen-mcp-server", version = "1.0.0" } },
                    "tools/list" => new
                    {
                        tools = new[] {
                    new { name = "generate_unit_tests", description = "Generate unit tests" },
                    new { name = "generate_integration_tests", description = "Generate integration tests" },
                    new { name = "create_mock_data", description = "Generate mock data" },
                    new { name = "generate_repository_tests", description = "Generate repository tests" },
                    new { name = "generate_test_project", description = "Generate test project file" },
                    new { name = "generate_mock_setup", description = "Generate Moq setup code" }
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
                "generate_unit_tests" => await TestGeneratorTools.GenerateUnitTests(args),
                "generate_integration_tests" => await TestGeneratorTools.GenerateIntegrationTests(args),
                "create_mock_data" => await TestGeneratorTools.CreateMockData(args),
                "generate_repository_tests" => await TestGeneratorTools.GenerateRepositoryTests(args),
                "generate_test_project" => await TestGeneratorTools.GenerateTestProject(args),
                "generate_mock_setup" => await TestGeneratorTools.GenerateMockSetup(args),
                _ => throw new Exception($"Unknown tool: {request.Params.Name}")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, Opts) } } };
        }
    }

}
