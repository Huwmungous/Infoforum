using System.Text.Json;

namespace FileTransferMcpServer.Protocol;

public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(ILogger<McpServer> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Returns a list of available tools supported by this server.
    /// </summary>
    public IEnumerable<object> GetAvailableTools()
    {
        return GetTools();
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        _logger.LogInformation("Handling request: {Method}", request.Method);

        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            _ => new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32601, Message = "Method not found" }
            }
        };
    }

    private static McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "filetransfermcpserver", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private static McpResponse HandleToolsList(McpRequest request)
    {
        object[] tools = GetTools();

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private static object[] GetTools()
    {
        return new object[]
        {
            new
            {
                name = "upload_file",
                description = "Upload a file from base64 content",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Destination file path" },
                        content = new { type = "string", description = "Base64 encoded file content" },
                        overwrite = new { type = "boolean", description = "Overwrite if file exists", @default = false }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new
            {
                name = "download_file",
                description = "Download a file as base64 content",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "create_zip",
                description = "Create a zip archive from a directory or file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Source directory or file path" },
                        zipPath = new { type = "string", description = "Destination zip file path" },
                        includeBaseDirectory = new { type = "boolean", description = "Include base directory in zip", @default = false }
                    },
                    required = new[] { "sourcePath", "zipPath" }
                }
            },
            new
            {
                name = "extract_zip",
                description = "Extract a zip archive",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        zipPath = new { type = "string", description = "Path to the zip file" },
                        extractPath = new { type = "string", description = "Destination extraction path" },
                        overwrite = new { type = "boolean", description = "Overwrite existing files", @default = false }
                    },
                    required = new[] { "zipPath", "extractPath" }
                }
            },
            new
            {
                name = "download_zip",
                description = "Download a zip file as base64 content",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        zipPath = new { type = "string", description = "Path to the zip file" }
                    },
                    required = new[] { "zipPath" }
                }
            },
            new
            {
                name = "upload_zip",
                description = "Upload a zip file from base64 content",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        zipPath = new { type = "string", description = "Destination zip file path" },
                        content = new { type = "string", description = "Base64 encoded zip content" },
                        overwrite = new { type = "boolean", description = "Overwrite if file exists", @default = false }
                    },
                    required = new[] { "zipPath", "content" }
                }
            },
            new
            {
                name = "list_zip_contents",
                description = "List contents of a zip archive",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        zipPath = new { type = "string", description = "Path to the zip file" }
                    },
                    required = new[] { "zipPath" }
                }
            },
            new
            {
                name = "get_file_base64",
                description = "Get file content as base64 string",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file" },
                        maxSize = new { type = "integer", description = "Maximum file size in bytes", @default = 10485760 }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "write_base64_to_file",
                description = "Write base64 content to a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Destination file path" },
                        base64 = new { type = "string", description = "Base64 encoded content" },
                        overwrite = new { type = "boolean", description = "Overwrite if file exists", @default = false }
                    },
                    required = new[] { "path", "base64" }
                }
            },
            new
            {
                name = "compress_files",
                description = "Compress multiple files into a zip archive",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        files = new { type = "array", items = new { type = "string" }, description = "List of file paths to compress" },
                        zipPath = new { type = "string", description = "Destination zip file path" }
                    },
                    required = new[] { "files", "zipPath" }
                }
            }
        };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var toolName = request.Params?.Name ?? "unknown";
        var arguments = request.Params?.Arguments ?? JsonDocument.Parse("{}").RootElement;

        try
        {
            object result = toolName switch
            {
                "upload_file" => await FileTransferTools.UploadFile(arguments),
                "download_file" => await FileTransferTools.DownloadFile(arguments),
                "create_zip" => await FileTransferTools.CreateZip(arguments),
                "extract_zip" => await FileTransferTools.ExtractZip(arguments),
                "download_zip" => await FileTransferTools.DownloadZip(arguments),
                "upload_zip" => await FileTransferTools.UploadZip(arguments),
                "list_zip_contents" => await FileTransferTools.ListZipContents(arguments),
                "get_file_base64" => await FileTransferTools.GetFileBase64(arguments),
                "write_base64_to_file" => await FileTransferTools.WriteBase64ToFile(arguments),
                "compress_files" => await FileTransferTools.CompressFiles(arguments),
                _ => throw new Exception($"Unknown tool: {toolName}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, _jsonOptions)
                        }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call: {ToolName}", toolName);
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}