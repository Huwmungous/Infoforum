using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GitMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();
await builder.Build().RunAsync();

namespace GitMcpServer
{

    public class McpServer(ILogger<McpServer> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Git MCP Server starting...");
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
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "git-mcp-server", version = "1.0.0" } },
                    "tools/list" => new
                    {
                        tools = new[] {
                    new { name = "git_init", description = "Initialize repository" },
                    new { name = "git_status", description = "Get repository status" },
                    new { name = "git_add", description = "Stage files" },
                    new { name = "git_commit", description = "Commit changes" },
                    new { name = "git_diff", description = "Show differences" },
                    new { name = "git_log", description = "Show commit history" },
                    new { name = "git_branch_list", description = "List branches" },
                    new { name = "git_branch_create", description = "Create branch" },
                    new { name = "git_checkout", description = "Checkout branch" },
                    new { name = "git_clone", description = "Clone repository" },
                    new { name = "git_pull", description = "Pull changes" }
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
                "git_init" => await GitTools.GitInit(args),
                "git_status" => await GitTools.GitStatus(args),
                "git_add" => await GitTools.GitAdd(args),
                "git_commit" => await GitTools.GitCommit(args),
                "git_diff" => await GitTools.GitDiff(args),
                "git_log" => await GitTools.GitLog(args),
                "git_branch_list" => await GitTools.GitBranchList(args),
                "git_branch_create" => await GitTools.GitBranchCreate(args),
                "git_checkout" => await GitTools.GitCheckout(args),
                "git_clone" => await GitTools.GitClone(args),
                "git_pull" => await GitTools.GitPull(args),
                _ => throw new Exception($"Unknown tool: {request.Params.Name}")
            };
            return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, Opts) } } };
        }
    }

}