using System.Text;
using System.Text.Json;
using DelphiAnalysisMcpServer.Services;
using Microsoft.Extensions.Logging;
using SfD.Global;
using SfD.Global.Logging;
using SfD.Mcp.Protocol.Models;

// Register code pages provider for Windows-1252 encoding (Delphi source files)
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("SfdLogger");

builder.Services.AddSingleton<SfdLogger>(sp =>
{
    var config = new SfdLoggerConfiguration
    {
        MinimumLogLevel = LogLevel.Information,
        EnableRemoteLogging = true,
        LoggerServiceUrl = "http://localhost:5310"
    };
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return new SfdLogger("CodeAnalysisMcpServer", config.LoggerServiceUrl, config.ClientId, config.Realm);
});

// CRITICAL: Use centralized port management via SfD.Global
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(port); });

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure logging
builder.Logging.AddConsole();

// Register services
builder.Services.AddSingleton<DelphiScannerService>();
builder.Services.AddSingleton<DprojParserService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<DatabaseExtractionService>();
builder.Services.AddSingleton<MethodExtractionService>();
builder.Services.AddSingleton<OutputGeneratorService>();
builder.Services.AddSingleton<CodeGenerationService>();
builder.Services.AddSingleton<McpToolHandler>();
// Register database services
builder.Services.AddSingleton<AnalysisRepository>();
builder.Services.AddSingleton<ProjectPersistenceService>();

// Configure HttpClient for Ollama
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10); // Long timeout for large translations
});

// Add CORS for flexibility
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// MCP SSE endpoint for connection
app.MapGet("/sse", async (HttpContext context, McpToolHandler toolHandler) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var hub = new SseHub();
    context.Items["hub"] = hub;

    // Send initial endpoint message
    var endpoint = $"{context.Request.Scheme}://{context.Request.Host}/mcp";
    await hub.PushAsync("endpoint", endpoint);

    // Keep connection alive
    try
    {
        await foreach (var chunk in hub.Reader.ReadAllAsync(context.RequestAborted))
        {
            await context.Response.WriteAsync(chunk);
            await context.Response.Body.FlushAsync();
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected
    }
});

// MCP JSON-RPC endpoint
app.MapPost("/mcp", async (HttpContext context, McpToolHandler toolHandler) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    Program.LogMcpRequestReceived(logger, body);

    McpRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<McpRequest>(body);
    }
    catch (JsonException ex)
    {
        return Results.Json(new McpResponse
        {
            Id = null,
            Error = new McpError { Code = -32700, Message = $"Parse error: {ex.Message}" }
        });
    }

    if (request is null)
    {
        return Results.Json(new McpResponse
        {
            Id = null,
            Error = new McpError { Code = -32600, Message = "Invalid request" }
        });
    }

    var response = new McpResponse { Id = request.Id };

    try
    {
        response.Result = request.Method switch
        {
            "initialize" => new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = false }
                },
                serverInfo = new
                {
                    name = "DelphiAnalysisMcpServer",
                    version = "1.0.0"
                }
            },
            "notifications/initialized" => new { },
            "tools/list" => new
            {
                tools = toolHandler.GetTools()
            },
            "tools/call" => await Program.HandleToolCallAsync(request, toolHandler),
            "ping" => new { },
            _ => throw new NotSupportedException($"Method not supported: {request.Method}")
        };
    }
    catch (NotSupportedException ex)
    {
        response.Error = new McpError { Code = -32601, Message = ex.Message };
    }
    catch (ArgumentException ex)
    {
        response.Error = new McpError { Code = -32602, Message = ex.Message };
    }
    catch (Exception ex)
    {
        Program.LogRequestProcessingError(logger, ex, request.Method);
        response.Error = new McpError { Code = -32603, Message = ex.Message };
    }

    return Results.Json(response);
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    server = "DelphiAnalysisMcpServer",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
}));

// Info endpoint
app.MapGet("/", () => Results.Ok(new
{
    name = "DelphiAnalysisMcpServer",
    version = "1.0.0",
    description = "MCP Server for analyzing and translating Delphi projects to C#",
    endpoints = new
    {
        sse = "/sse",
        mcp = "/mcp",
        health = "/health"
    },
    tools = Program.AvailableTools
}));

app.Run($"http://0.0.0.0:{port}");

// Partial class to hold static members (required for top-level statements)
partial class Program
{
    // Cached JsonSerializerOptions for tool result serialization (CA1869)
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    // Cached tools array to avoid repeated allocations (CA1861)
    internal static readonly string[] AvailableTools =
    [
        "list_delphi_projects",
        "describe_single_project",
        "scan_delphi_project",
        "parse_dproj",
        "analyze_unit",
        "analyze_database_operations",
        "generate_repository",
        "generate_controller",
        "generate_react_component",
        "translate_unit",
        "translate_project",
        "configure_translation",
        "generate_output",
        "get_session_status",
        "list_sessions"
    ];

    // LoggerMessage definitions for high-performance logging (CA1873)
    [LoggerMessage(Level = LogLevel.Debug, Message = "Received MCP request: {Body}")]
    internal static partial void LogMcpRequestReceived(ILogger logger, string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing request {Method}")]
    internal static partial void LogRequestProcessingError(ILogger logger, Exception ex, string method);

    internal static async Task<object> HandleToolCallAsync(McpRequest request, McpToolHandler toolHandler)
    {
        var toolName = request.Params?.Name
            ?? throw new ArgumentException("Tool name is required");

        var result = await toolHandler.HandleToolCallAsync(toolName, request.Params?.Arguments);

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(result, s_indentedJsonOptions)
                }
            }
        };
    }
}
