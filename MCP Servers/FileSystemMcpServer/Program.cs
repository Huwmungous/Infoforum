using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FileSystemMcpServer;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<McpServer>();

var host = builder.Build();
await host.RunAsync();

namespace FileSystemMcpServer
{
    public class McpServer : BackgroundService
    {
        private readonly ILogger<McpServer> _logger;
        private readonly FileSystemTools _tools;

        public McpServer(ILogger<McpServer> logger)
        {
            _logger = logger;
            _tools = new FileSystemTools();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FileSystem MCP Server starting...");

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
                    name = "filesystem-mcp-server",
                    version = "1.0.0"
                }
            });
        }

        private object HandleToolsList()
        {
            var tools = new[]
            {
                new { name = "read_file", description = "Read the contents of a file" },
                new { name = "write_file", description = "Write content to a file" },
                new { name = "append_file", description = "Append content to a file" },
                new { name = "delete_file", description = "Delete a file" },
                new { name = "list_directory", description = "List files and directories" },
                new { name = "search_files", description = "Search for text within files" },
                new { name = "get_file_info", description = "Get file or directory information" },
                new { name = "create_directory", description = "Create a new directory" },
                new { name = "move_file", description = "Move or rename a file" },
                new { name = "copy_file", description = "Copy a file" },
                new { name = "file_exists", description = "Check if a file exists" },
                new { name = "directory_exists", description = "Check if a directory exists" }
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
                "read_file" => await _tools.ReadFile(args),
                "write_file" => await _tools.WriteFile(args),
                "append_file" => await _tools.AppendFile(args),
                "delete_file" => await _tools.DeleteFile(args),
                "list_directory" => await _tools.ListDirectory(args),
                "search_files" => await _tools.SearchFiles(args),
                "get_file_info" => await _tools.GetFileInfo(args),
                "create_directory" => await _tools.CreateDirectory(args),
                "move_file" => await _tools.MoveFile(args),
                "copy_file" => await _tools.CopyFile(args),
                "file_exists" => await _tools.FileExists(args),
                "directory_exists" => await _tools.DirectoryExists(args),
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
