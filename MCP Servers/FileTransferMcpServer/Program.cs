namespace FileTransferMcpServer;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<McpServer>();
        await builder.Build().RunAsync();
    }
}

public class McpServer(ILogger<McpServer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("File Transfer MCP Server starting...");
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
                    logger.LogError(ex, "Error processing request");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new McpResponse { Jsonrpc = "2.0", Error = new McpError { Code = -32603, Message = ex.Message } }));
                }
            }
        }
        catch(Exception ex) { logger.LogError(ex, "Fatal error"); }
    }

    private static async Task<McpResponse> HandleRequest(McpRequest request)
    {
        try
        {
            object? result = request.Method switch
            {
                "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = "filetransfer-mcp-server", version = "1.0.0" } },
                "tools/list" => new
                {
                    tools = new[] {
                                new { name = "upload_file", description = "Upload a file from base64 content" },
                                new { name = "download_file", description = "Download a file as base64" },
                                new { name = "create_zip", description = "Create a zip archive from directory or files" },
                                new { name = "extract_zip", description = "Extract a zip archive" },
                                new { name = "download_zip", description = "Download a zip file as base64" },
                                new { name = "upload_zip", description = "Upload a zip file from base64" },
                                new { name = "list_zip_contents", description = "List contents of a zip file" },
                                new { name = "get_file_base64", description = "Get file content as base64" },
                                new { name = "write_base64_to_file", description = "Write base64 content to file" },
                                new { name = "compress_files", description = "Compress multiple files into a zip" }
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
            "upload_file" => await FileTransferTools.UploadFile(args),
            "download_file" => await FileTransferTools.DownloadFile(args),
            "create_zip" => await FileTransferTools.CreateZip(args),
            "extract_zip" => await FileTransferTools.ExtractZip(args),
            "download_zip" => await FileTransferTools.DownloadZip(args),
            "upload_zip" => await FileTransferTools.UploadZip(args),
            "list_zip_contents" => await FileTransferTools.ListZipContents(args),
            "get_file_base64" => await FileTransferTools.GetFileBase64(args),
            "write_base64_to_file" => await FileTransferTools.WriteBase64ToFile(args),
            "compress_files" => await FileTransferTools.CompressFiles(args),
            _ => throw new Exception($"Unknown tool: {request.Params.Name}")
        };
        return new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, SerializerOptions) } } };
    }
}
