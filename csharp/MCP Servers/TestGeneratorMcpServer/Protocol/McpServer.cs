using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace TestGeneratorMcpServer.Protocol;

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
                serverInfo = new { name = "testgeneratormcpserver", version = "1.0.0" },
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
                name = "generate_unit_tests",
                description = "Generate xUnit unit tests for a class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Name of the class to test" },
                        methods = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string", description = "Method name" },
                                    returnType = new { type = "string", description = "Return type" }
                                }
                            }
                        },
                        framework = new { type = "string", description = "Test framework (xUnit, NUnit)", @default = "xUnit" }
                    },
                    required = new[] { "className", "methods" }
                }
            },
            new
            {
                name = "generate_integration_tests",
                description = "Generate integration tests for a class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Name of the class to test" },
                        databaseTests = new { type = "boolean", description = "Include database test setup", @default = false }
                    },
                    required = new[] { "className" }
                }
            },
            new
            {
                name = "create_mock_data",
                description = "Generate mock data for an entity",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        entityName = new { type = "string", description = "Name of the entity" },
                        count = new { type = "integer", description = "Number of mock records", @default = 10 },
                        properties = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    name = new { type = "string" },
                                    type = new { type = "string" }
                                }
                            }
                        }
                    },
                    required = new[] { "entityName", "properties" }
                }
            },
            new
            {
                name = "generate_repository_tests",
                description = "Generate repository pattern tests",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        repositoryName = new { type = "string", description = "Name of the repository" },
                        entityName = new { type = "string", description = "Name of the entity" }
                    },
                    required = new[] { "repositoryName", "entityName" }
                }
            },
            new
            {
                name = "generate_test_project",
                description = "Generate a test project file (.csproj)",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectName = new { type = "string", description = "Name of the test project" },
                        framework = new { type = "string", description = "Test framework", @default = "xUnit" }
                    },
                    required = new[] { "projectName" }
                }
            },
            new
            {
                name = "generate_mock_setup",
                description = "Generate Moq setup code for an interface",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        interfaceName = new { type = "string", description = "Name of the interface" },
                        methods = new { type = "array", items = new { type = "string" }, description = "Method names" }
                    },
                    required = new[] { "interfaceName", "methods" }
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
                "generate_unit_tests" => await TestGeneratorTools.GenerateUnitTests(arguments),
                "generate_integration_tests" => await TestGeneratorTools.GenerateIntegrationTests(arguments),
                "create_mock_data" => await TestGeneratorTools.CreateMockData(arguments),
                "generate_repository_tests" => await TestGeneratorTools.GenerateRepositoryTests(arguments),
                "generate_test_project" => await TestGeneratorTools.GenerateTestProject(arguments),
                "generate_mock_setup" => await TestGeneratorTools.GenerateMockSetup(arguments),
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