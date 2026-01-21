using SfD.Mcp.Protocol.Models;
using System.Text.Json;
using UiComponentConverterMcpServer.Services;

namespace UiComponentConverterMcpServer.Protocol;

public class McpServer
{
    private readonly UiConverterService _converterService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(UiConverterService converterService, ILogger<McpServer> logger)
    {
        _converterService = converterService;
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

        await Task.CompletedTask;
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => HandleToolCall(request),
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
                serverInfo = new { name = "uicomponentconvertermcpserver", version = "1.0.0" },
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
                name = "parse_delphi_form",
                description = "Parse Delphi .dfm file and extract component tree",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "analyze_ui_components",
                description = "Analyze UI components and generate inventory",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "map_to_react",
                description = "Generate React component from Delphi form",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "map_to_angular",
                description = "Generate Angular component from Delphi form",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "map_to_blazor",
                description = "Generate Blazor component from Delphi form",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "extract_event_handlers",
                description = "Extract all event handlers from form",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "generate_css_layout",
                description = "Generate CSS from Delphi layout properties",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new
            {
                name = "create_state_model",
                description = "Create state management model from form",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            }
        };
    }

    private McpResponse HandleToolCall(McpRequest request)
    {
        var toolName = request.Params?.Name ?? "unknown";
        var arguments = request.Params?.Arguments ?? JsonDocument.Parse("{}").RootElement;

        try
        {
            var dfmContent = arguments.GetProperty("dfmContent").GetString()!;
            var form = _converterService.ParseDelphiForm(dfmContent);

            object result = toolName switch
            {
                "parse_delphi_form" => form,
                "analyze_ui_components" => _converterService.AnalyzeUiComponents(form),
                "map_to_react" => _converterService.MapToReact(form),
                "map_to_angular" => _converterService.MapToAngular(form),
                "map_to_blazor" => _converterService.MapToBlazor(form),
                "extract_event_handlers" => _converterService.ExtractEventHandlers(form),
                "generate_css_layout" => _converterService.GenerateCssLayout(),
                "create_state_model" => _converterService.CreateStateModel(form),
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