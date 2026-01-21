using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace DocumentationMcpServer.Protocol;

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
                serverInfo = new { name = "documentationmcpserver", version = "1.0.0" },
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
                name = "generate_readme",
                description = "Generate a README.md file for a project",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectName = new { type = "string", description = "Name of the project" },
                        description = new { type = "string", description = "Project description" },
                        features = new { type = "array", items = new { type = "string" }, description = "List of project features" }
                    },
                    required = new[] { "projectName" }
                }
            },
            new
            {
                name = "generate_api_docs",
                description = "Generate API documentation for a class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Name of the class" },
                        methods = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    description = new { type = "string" },
                                    parameters = new { type = "array", items = new { type = "string" } },
                                    returnType = new { type = "string" }
                                }
                            }
                        }
                    },
                    required = new[] { "className", "methods" }
                }
            },
            new
            {
                name = "create_migration_report",
                description = "Create a migration progress report",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectName = new { type = "string", description = "Name of the migration project" },
                        filesConverted = new { type = "integer", description = "Number of files converted" },
                        filesTotal = new { type = "integer", description = "Total number of files" },
                        issues = new { type = "array", items = new { type = "string" }, description = "List of issues found" }
                    },
                    required = new[] { "projectName" }
                }
            },
            new
            {
                name = "generate_changelog",
                description = "Generate a changelog entry",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        version = new { type = "string", description = "Version number" },
                        changes = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    type = new { type = "string", description = "Change type (Added, Fixed, Changed, etc.)" },
                                    description = new { type = "string", description = "Change description" }
                                }
                            }
                        }
                    },
                    required = new[] { "version", "changes" }
                }
            },
            new
            {
                name = "generate_xml_comments",
                description = "Generate XML documentation comments for a method",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        methodName = new { type = "string", description = "Name of the method" },
                        summary = new { type = "string", description = "Method summary" },
                        parameters = new { type = "array", items = new { type = "string" }, description = "Parameter names" },
                        returnDescription = new { type = "string", description = "Description of return value" }
                    },
                    required = new[] { "methodName" }
                }
            },
            new
            {
                name = "generate_class_diagram",
                description = "Generate a Mermaid class diagram",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        classes = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Class name" },
                                    properties = new { type = "array", items = new { type = "string" }, description = "Class properties" },
                                    methods = new { type = "array", items = new { type = "string" }, description = "Class methods" }
                                }
                            }
                        }
                    },
                    required = new[] { "classes" }
                }
            },
            new
            {
                name = "generate_swagger_documentation",
                description = "Generate Swagger/OpenAPI documentation for C# endpoints (controller or minimal API style)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        endpointName = new { type = "string", description = "Name of the endpoint" },
                        httpMethod = new { type = "string", description = "HTTP method (Get, Post, Put, Delete, etc.)", @default = "Get" },
                        route = new { type = "string", description = "Route template" },
                        summary = new { type = "string", description = "Brief summary of the endpoint" },
                        description = new { type = "string", description = "Detailed description" },
                        tags = new { type = "array", items = new { type = "string" }, description = "Swagger tags" },
                        apiStyle = new { type = "string", @enum = new[] { "minimal", "controller" }, description = "API style: 'minimal' or 'controller'", @default = "minimal" },
                        parameters = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Parameter name" },
                                    type = new { type = "string", description = "Parameter C# type" },
                                    description = new { type = "string", description = "Parameter description" },
                                    required = new { type = "boolean", description = "Whether parameter is required" },
                                    location = new { type = "string", @enum = new[] { "query", "path", "header", "body" }, description = "Parameter location" }
                                }
                            }
                        },
                        responses = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    statusCode = new { type = "integer", description = "HTTP status code" },
                                    description = new { type = "string", description = "Response description" },
                                    returnType = new { type = "string", description = "C# return type (optional)" }
                                }
                            }
                        }
                    },
                    required = new[] { "endpointName", "summary" }
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
                "generate_readme" => await DocumentationTools.GenerateReadme(arguments),
                "generate_api_docs" => await DocumentationTools.GenerateApiDocs(arguments),
                "create_migration_report" => await DocumentationTools.CreateMigrationReport(arguments),
                "generate_changelog" => await DocumentationTools.GenerateChangelog(arguments),
                "generate_xml_comments" => await DocumentationTools.GenerateXmlComments(arguments),
                "generate_class_diagram" => await DocumentationTools.GenerateClassDiagram(arguments),
                "generate_swagger_documentation" => await DocumentationTools.GenerateSwaggerDocumentation(arguments),
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