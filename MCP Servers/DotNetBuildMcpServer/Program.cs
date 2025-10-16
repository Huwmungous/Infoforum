using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotNetBuildMcpServer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<McpServer>();

var host = builder.Build();
await host.RunAsync();

namespace DotNetBuildMcpServer
{
    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private readonly ILogger<McpServer> _logger = logger;
        private readonly DotNetBuildTools _tools = new();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DotNet Build MCP Server starting...");

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
                    name = "dotnetbuild-mcp-server",
                    version = "1.0.0"
                }
            });
        }

        private static object HandleToolsList()
        {
            var tools = new[]
            {
                new { name = "dotnet_build", description = "Build a .NET project" },
                new { name = "dotnet_clean", description = "Clean build output" },
                new { name = "dotnet_restore", description = "Restore NuGet packages" },
                new { name = "dotnet_test", description = "Run unit tests" },
                new { name = "dotnet_publish", description = "Publish the project" },
                new { name = "get_build_errors", description = "Get compilation errors" },
                new { name = "get_build_warnings", description = "Get compilation warnings" },
                new { name = "dotnet_add_package", description = "Add a NuGet package" },
                new { name = "dotnet_remove_package", description = "Remove a NuGet package" },
                new { name = "dotnet_list_packages", description = "List installed packages" },
                new { name = "analyze_code_syntax", description = "Analyze C# code syntax" },
                new { name = "validate_csharp_code", description = "Validate C# code" }
            };

            return new { tools };
        }

        private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() { WriteIndented = true };

        private static async Task<object> HandleToolCall(McpRequest request)
        {
            if(request.Params?.Arguments == null)
                throw new Exception("Missing arguments");

            var toolName = request.Params.Name;
            var args = request.Params.Arguments.Value;

            var result = toolName switch
            {
                "dotnet_build" => await DotNetBuildTools.DotNetBuild(args),
                "dotnet_clean" => await DotNetBuildTools.DotNetClean(args),
                "dotnet_restore" => await DotNetBuildTools.DotNetRestore(args),
                "dotnet_test" => await DotNetBuildTools.DotNetTest(args),
                "dotnet_publish" => await DotNetBuildTools.DotNetPublish(args),
                "get_build_errors" => await DotNetBuildTools.GetBuildErrors(args), // Fixed here
                "get_build_warnings" => await DotNetBuildTools.GetBuildWarnings(args), // Fixed here
                "dotnet_add_package" => await DotNetBuildTools.DotNetAddPackage(args),
                "dotnet_remove_package" => await DotNetBuildTools.DotNetRemovePackage(args),
                "dotnet_list_packages" => await DotNetBuildTools.DotNetListPackages(args),
                "analyze_code_syntax" => await DotNetBuildTools.AnalyzeCodeSyntax(args),
                "validate_csharp_code" => await DotNetBuildTools.ValidateCSharpCode(args),
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
