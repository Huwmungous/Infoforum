using UiComponentConverterMcpServer.Models;
using UiComponentConverterMcpServer.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UI Component Converter MCP Server starting");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) break;

                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                    if (request == null) continue;

                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    
                    await Console.Out.WriteLineAsync(responseJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request error");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested");
        }
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
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

    private McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "ui-component-converter-mcp", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new[]
        {
            new ToolInfo
            {
                Name = "parse_delphi_form",
                Description = "Parse Delphi .dfm file and extract component tree",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string", description = "Content of .dfm file" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "analyze_ui_components",
                Description = "Analyze UI components and generate inventory",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "map_to_react",
                Description = "Generate React component from Delphi form",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "map_to_angular",
                Description = "Generate Angular component from Delphi form",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "map_to_blazor",
                Description = "Generate Blazor component from Delphi form",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "extract_event_handlers",
                Description = "Extract all event handlers from form",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "generate_css_layout",
                Description = "Generate CSS from Delphi layout properties",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            },
            new ToolInfo
            {
                Name = "create_state_model",
                Description = "Create state management model from form",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dfmContent = new { type = "string" }
                    },
                    required = new[] { "dfmContent" }
                }
            }
        };

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private McpResponse HandleToolCall(McpRequest request)
    {
        var paramsElement = (JsonElement)request.Params!;
        var toolName = paramsElement.GetProperty("name").GetString()!;
        var arguments = paramsElement.GetProperty("arguments");

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
                "generate_css_layout" => _converterService.GenerateCssLayout(form),
                "create_state_model" => _converterService.CreateStateModel(form),
                _ => throw new InvalidOperationException("Unknown tool")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(result, _jsonOptions) }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}