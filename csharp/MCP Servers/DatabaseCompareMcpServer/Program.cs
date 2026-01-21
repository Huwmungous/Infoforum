using DatabaseCompareMcpServer.Protocol;
using SfD.Global;
using SfD.Global.Logging;
using System.Reflection;
using System.Text.Json;

// ──────────────────────────────────────────────
// Builder
// ──────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Logging (CRITICAL)
// ──────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ──────────────────────────────────────────────
// HttpClient (required for SfdLogger + parity)
// ──────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("SfdLogger");

// ──────────────────────────────────────────────
// SfdLogger (same pattern as Delphi)
// ──────────────────────────────────────────────
builder.Services.AddSingleton<SfdLogger>(sp =>
{
    var config = new SfdLoggerConfiguration
    {
        MinimumLogLevel = LogLevel.Information,
        EnableRemoteLogging = true
    };

    return new SfdLogger(
        "DatabaseCompareMcpServer",
        config.LoggerServiceUrl,
        config.ClientId,
        config.Realm);
});

// ──────────────────────────────────────────────
// Swagger
// ──────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var asm = Assembly.GetExecutingAssembly();
    var xmlName = $"{asm.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// ──────────────────────────────────────────────
// Port resolution (SfD.Global)
// ──────────────────────────────────────────────
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(port));

// ──────────────────────────────────────────────
// MCP Server
// ──────────────────────────────────────────────
builder.Services.AddSingleton<McpServer>();

var app = builder.Build();

// ──────────────────────────────────────────────
// Swagger UI
// ──────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DatabaseCompareMcpServer v1");
    c.RoutePrefix = "swagger";
});

// ──────────────────────────────────────────────
// SSE ENDPOINT (THIS WAS MISSING)
// ──────────────────────────────────────────────
app.MapGet("/sse", async (HttpContext context) =>
{
    app.Logger.LogInformation("SSE client connected");

    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    var endpoint =
        $"{context.Request.Scheme}://{context.Request.Host}/mcp";

    // MCP endpoint discovery message
    await context.Response.WriteAsync(
        $"event: endpoint\ndata: {endpoint}\n\n");

    await context.Response.Body.FlushAsync();

    try
    {
        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        app.Logger.LogInformation("SSE client disconnected");
    }
});

// ──────────────────────────────────────────────
// Health check
// ──────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "database-compare-mcp",
    port,
    version = "1.0.0"
}));

// ──────────────────────────────────────────────
// Root info
// ──────────────────────────────────────────────
app.MapGet("/", (McpServer mcpServer) =>
{
    return Results.Ok(new
    {
        name = "DatabaseCompareMcpServer",
        version = "1.0.0",
        description = "MCP Server for DB operations",
        endpoints = new
        {
            sse = "/sse",
            mcp = "/mcp",
            health = "/health"
        },
        tools = mcpServer.GetAvailableTools()
    });
});

// ──────────────────────────────────────────────
// MCP JSON-RPC endpoint
// ──────────────────────────────────────────────
app.MapPost("/mcp", async (HttpContext context) =>
{
    var mcpServer = context.RequestServices.GetRequiredService<McpServer>();

    string body;
    using (var reader = new StreamReader(context.Request.Body))
        body = await reader.ReadToEndAsync();

    app.Logger.LogInformation("Received MCP request: {Body}", body);

    string responseJson;
    try
    {
        responseJson = await mcpServer.HandleRequestStringAsync(body);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled MCP error");
        responseJson = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            error = new { code = -32603, message = "Internal error" }
        });
    }

    app.Logger.LogInformation("Sending MCP response: {Response}", responseJson);

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(responseJson);
});

// ──────────────────────────────────────────────
// Startup
// ──────────────────────────────────────────────
app.Logger.LogInformation("DatabaseCompare MCP Server listening on port {Port}", port);
app.Run();
