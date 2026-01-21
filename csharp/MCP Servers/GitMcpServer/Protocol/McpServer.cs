using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace GitMcpServer.Protocol;

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
                serverInfo = new { name = "gitmcpserver", version = "1.0.0" },
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
                name = "git_init",
                description = "Initialize a new Git repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path where to initialize the repository" }
                    },
                    required = new[] { "path" }
                }
            },
            new
            {
                name = "git_status",
                description = "Get the status of a Git repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" }
                    },
                    required = new[] { "repoPath" }
                }
            },
            new
            {
                name = "git_add",
                description = "Stage files for commit",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        filePattern = new { type = "string", description = "File pattern to stage (e.g., '.' for all, '*.cs' for C# files)" }
                    },
                    required = new[] { "repoPath", "filePattern" }
                }
            },
            new
            {
                name = "git_commit",
                description = "Commit staged changes",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        message = new { type = "string", description = "Commit message" },
                        author = new { type = "string", description = "Author name", @default = "MCP User" },
                        email = new { type = "string", description = "Author email", @default = "mcp@localhost" }
                    },
                    required = new[] { "repoPath", "message" }
                }
            },
            new
            {
                name = "git_diff",
                description = "Show differences in the repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        file = new { type = "string", description = "Specific file to diff (optional)" }
                    },
                    required = new[] { "repoPath" }
                }
            },
            new
            {
                name = "git_log",
                description = "Show commit history",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        maxCount = new { type = "integer", description = "Maximum number of commits to show", @default = 10 }
                    },
                    required = new[] { "repoPath" }
                }
            },
            new
            {
                name = "git_branch_list",
                description = "List all branches in the repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" }
                    },
                    required = new[] { "repoPath" }
                }
            },
            new
            {
                name = "git_branch_create",
                description = "Create a new branch",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        branchName = new { type = "string", description = "Name of the new branch" }
                    },
                    required = new[] { "repoPath", "branchName" }
                }
            },
            new
            {
                name = "git_checkout",
                description = "Checkout a branch",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" },
                        branchName = new { type = "string", description = "Name of the branch to checkout" }
                    },
                    required = new[] { "repoPath", "branchName" }
                }
            },
            new
            {
                name = "git_clone",
                description = "Clone a remote repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new { type = "string", description = "URL of the remote repository" },
                        localPath = new { type = "string", description = "Local path where to clone" }
                    },
                    required = new[] { "url", "localPath" }
                }
            },
            new
            {
                name = "git_pull",
                description = "Pull changes from remote repository",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repoPath = new { type = "string", description = "Path to the Git repository" }
                    },
                    required = new[] { "repoPath" }
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
                "git_init" => await GitTools.GitInit(arguments),
                "git_status" => await GitTools.GitStatus(arguments),
                "git_add" => await GitTools.GitAdd(arguments),
                "git_commit" => await GitTools.GitCommit(arguments),
                "git_diff" => await GitTools.GitDiff(arguments),
                "git_log" => await GitTools.GitLog(arguments),
                "git_branch_list" => await GitTools.GitBranchList(arguments),
                "git_branch_create" => await GitTools.GitBranchCreate(arguments),
                "git_checkout" => await GitTools.GitCheckout(arguments),
                "git_clone" => await GitTools.GitClone(arguments),
                "git_pull" => await GitTools.GitPull(arguments),
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