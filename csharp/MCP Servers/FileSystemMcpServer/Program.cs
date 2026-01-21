using FileSystemMcpServer.Protocol; 
using SfD.Global; using SfD.Global.Logging;
using SfD.Mcp.Protocol.Models;
using System.Reflection;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SfdLogger>(sp =>
{
    var config = new SfdLoggerConfiguration
    {
        MinimumLogLevel = LogLevel.Information,
        EnableRemoteLogging = true // or false, as needed
    };
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>(); 
    return new SfdLogger("FileSystemMcpServer", config.LoggerServiceUrl, config.ClientId, config.Realm);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var asm = Assembly.GetExecutingAssembly();
    var xmlName = $"{asm.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))           // don't throw if it’s missing on Linux
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddSingleton<McpServer>();



var app = builder.Build(); 

var sseHub = new SseHub();

// Info endpoint for FileSystem MCP Server
app.MapGet("/", (IServiceProvider services) =>
{
    var mcpServer = services.GetRequiredService<McpServer>();
    var tools = mcpServer.GetAvailableTools();

    return Results.Ok(new
    {
        name = "FileSystemMcpServer",
        version = "1.0.0",
        description = "MCP Server for file system operations",
        endpoints = new
        {
            sse = "/sse",
            rpc = "/rpc",
            health = "/health",
            toolslist = "/toolslist"
        },
        tools
    });
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "filesystem-mcp",
    port,
    version = "1.0.0"
}));



app.MapGet("/toolslist", (IServiceProvider services) =>
{
    var mcpServer = services.GetRequiredService<McpServer>();
    var tools = mcpServer.GetAvailableTools();
    return Results.Ok(tools);
})
.WithName("GetAvailableTools")
.WithTags("Tools")
.Produces(StatusCodes.Status200OK, contentType: "application/json");

app.MapGet("/sse", async ctx =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";
    ctx.Response.ContentType = "text/event-stream";
    var reader = sseHub.Reader;
    await foreach (var evt in reader.ReadAllAsync(ctx.RequestAborted))
    {
        await ctx.Response.WriteAsync(evt, ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
});

app.MapPost("/rpc", async (HttpContext ctx, McpServer mcp) =>
{
    var req = await JsonSerializer.DeserializeAsync<McpRequest>(ctx.Request.Body);
    if (req is null) return Results.BadRequest(new { error = "invalid request" });
    var resp = await mcp.HandleRequestAsync(req);
    await sseHub.PushAsync("rpc", resp);
    return Results.Json(resp);
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileSystemMcpServer API v1");
    c.RoutePrefix = "swagger";
});


app.Logger.LogInformation("FileSystem MCP Server listening on port {Port}", port);
app.Run($"http://0.0.0.0:{port}");
