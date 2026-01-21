using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace DotNetBuildMcpServer.Protocol;

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
                serverInfo = new { name = "dotnetbuildmcpserver", version = "1.0.0" },
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
                name = "dotnet_build",
                description = "Build a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" },
                        configuration = new { type = "string", description = "Build configuration (Debug/Release)", @default = "Debug" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet_clean",
                description = "Clean build outputs of a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet_restore",
                description = "Restore NuGet packages for a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet_test",
                description = "Run tests in a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" },
                        filter = new { type = "string", description = "Test filter expression" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet_publish",
                description = "Publish a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" },
                        configuration = new { type = "string", description = "Build configuration", @default = "Release" },
                        runtime = new { type = "string", description = "Target runtime identifier (e.g., linux-x64, win-x64)" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "get_build_errors",
                description = "Get build errors from a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "get_build_warnings",
                description = "Get build warnings from a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "dotnet_add_package",
                description = "Add a NuGet package to a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" },
                        packageName = new { type = "string", description = "Name of the NuGet package" },
                        version = new { type = "string", description = "Package version (optional)" }
                    },
                    required = new[] { "projectPath", "packageName" }
                }
            },
            new
            {
                name = "dotnet_remove_package",
                description = "Remove a NuGet package from a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" },
                        packageName = new { type = "string", description = "Name of the NuGet package" }
                    },
                    required = new[] { "projectPath", "packageName" }
                }
            },
            new
            {
                name = "dotnet_list_packages",
                description = "List all NuGet packages in a .NET project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to the .csproj file" }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new
            {
                name = "analyze_code_syntax",
                description = "Analyze C# code syntax for errors and warnings",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the C# file" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "validate_csharp_code",
                description = "Validate C# code string for syntax errors",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string", description = "C# code to validate" }
                    },
                    required = new[] { "code" }
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
                "dotnet_build" => await DotNetBuildTools.DotNetBuild(arguments),
                "dotnet_clean" => await DotNetBuildTools.DotNetClean(arguments),
                "dotnet_restore" => await DotNetBuildTools.DotNetRestore(arguments),
                "dotnet_test" => await DotNetBuildTools.DotNetTest(arguments),
                "dotnet_publish" => await DotNetBuildTools.DotNetPublish(arguments),
                "get_build_errors" => await DotNetBuildTools.GetBuildErrors(arguments),
                "get_build_warnings" => await DotNetBuildTools.GetBuildWarnings(arguments),
                "dotnet_add_package" => await DotNetBuildTools.DotNetAddPackage(arguments),
                "dotnet_remove_package" => await DotNetBuildTools.DotNetRemovePackage(arguments),
                "dotnet_list_packages" => await DotNetBuildTools.DotNetListPackages(arguments),
                "analyze_code_syntax" => await DotNetBuildTools.AnalyzeCodeSyntax(arguments),
                "validate_csharp_code" => await DotNetBuildTools.ValidateCSharpCode(arguments),
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