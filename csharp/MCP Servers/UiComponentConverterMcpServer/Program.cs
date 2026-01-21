using SfD.Global; using SfD.Global.Logging;
using SfD.Mcp.Protocol.Models;
using System.Reflection;
using System.Text.Json;
using UiComponentConverterMcpServer.Protocol;
using UiComponentConverterMcpServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SfdLogger>(sp =>
{
    var config = new SfdLoggerConfiguration
    {
        MinimumLogLevel = LogLevel.Information,
        EnableRemoteLogging = true // or false, as needed
    };
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>(); 
    return new SfdLogger("UiComponentConverterMcpServer", config.LoggerServiceUrl, config.ClientId, config.Realm);
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

builder.Services.AddSingleton<UiConverterService>();
builder.Services.AddSingleton<McpServer>(); 


var app = builder.Build(); 

var sseHub = new SseHub();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "uicomponentconvertor-mcp",
    port,
    version = "1.0.0"
}));

app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Redirect("/health"));

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

app.MapGet("/toolslist", (IServiceProvider services) =>
{
    var mcpServer = services.GetRequiredService<McpServer>();
    var tools = mcpServer.GetAvailableTools();
    return Results.Ok(tools);
})
.WithName("GetAvailableTools")
.WithTags("Tools")
.Produces(StatusCodes.Status200OK, contentType: "application/json");

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UiComponentConverterMcpServer v1");
    c.RoutePrefix = "swagger";
});


app.Logger.LogInformation("UI Component Converter MCP Server listening on port {Port}", port);
app.Run();