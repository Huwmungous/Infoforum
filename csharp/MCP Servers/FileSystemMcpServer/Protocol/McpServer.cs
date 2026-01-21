using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace FileSystemMcpServer.Protocol;

public class McpServer
{
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly FileSystemTools _tools;

    public McpServer(ILogger<McpServer> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        _tools = new FileSystemTools();
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
                serverInfo = new { name = "filesystemmcpserver", version = "1.0.0" },
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
                name = "read_file",
                description = "Read contents of a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file" },
                        encoding = new { type = "string", description = "File encoding (utf-8, ascii, utf-16, utf-32)", @default = "utf-8" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "write_file",
                description = "Write content to a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file" },
                        content = new { type = "string", description = "Content to write" },
                        encoding = new { type = "string", description = "File encoding", @default = "utf-8" }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new
            {
                name = "append_file",
                description = "Append content to a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file" },
                        content = new { type = "string", description = "Content to append" }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new
            {
                name = "delete_file",
                description = "Delete a file",
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
                name = "list_directory",
                description = "List contents of a directory",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the directory" },
                        pattern = new { type = "string", description = "File pattern filter", @default = "*" },
                        recursive = new { type = "boolean", description = "Search recursively", @default = false }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "search_files",
                description = "Search for text within files",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to search in" },
                        searchTerm = new { type = "string", description = "Text to search for" },
                        filePattern = new { type = "string", description = "File pattern filter", @default = "*" },
                        recursive = new { type = "boolean", description = "Search recursively", @default = true }
                    },
                    required = new[] { "path", "searchTerm" }
                }
            },
            new
            {
                name = "get_file_info",
                description = "Get detailed information about a file or directory",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file or directory" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "create_directory",
                description = "Create a new directory",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the directory" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "move_file",
                description = "Move or rename a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Source file path" },
                        destPath = new { type = "string", description = "Destination file path" }
                    },
                    required = new[] { "sourcePath", "destPath" }
                }
            },
            new
            {
                name = "copy_file",
                description = "Copy a file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Source file path" },
                        destPath = new { type = "string", description = "Destination file path" }
                    },
                    required = new[] { "sourcePath", "destPath" }
                }
            },
            new
            {
                name = "file_exists",
                description = "Check if a file exists",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to check" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "directory_exists",
                description = "Check if a directory exists",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to check" }
                    },
                    required = new[] { "path" }
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
                "read_file" => await _tools.ReadFile(arguments),
                "write_file" => await _tools.WriteFile(arguments),
                "append_file" => await _tools.AppendFile(arguments),
                "delete_file" => await _tools.DeleteFile(arguments),
                "list_directory" => await _tools.ListDirectory(arguments),
                "search_files" => await _tools.SearchFiles(arguments),
                "get_file_info" => await _tools.GetFileInfo(arguments),
                "create_directory" => await _tools.CreateDirectory(arguments),
                "move_file" => await _tools.MoveFile(arguments),
                "copy_file" => await _tools.CopyFile(arguments),
                "file_exists" => await _tools.FileExists(arguments),
                "directory_exists" => await _tools.DirectoryExists(arguments),
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