 
using PlaywrightMcpServer.Models;
using PlaywrightMcpServer.Protocol; 
using PlaywrightMcpServer.Services;
using SfD.Global;
using SfD.Mcp.Protocol.Models;
using System.Text.Json; 

var builder = WebApplication.CreateBuilder(args);
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Existing service registrations
builder.Services.AddSingleton<PlaywrightService>();
        builder.Services.AddSingleton<McpServer>();

// Add Swagger for development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "PlaywrightMcpServer API", Version = "v1" });
    });
}



var app = builder.Build();

var sseHub = new SseHub();

app.MapGet("/", () => Results.Json(new { ok = true, server = "PlaywrightMcpServer" }));
app.MapGet("/health", () => Results.Ok("OK"));

app.MapGet("/sse", async (HttpContext ctx) =>
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


// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlaywrightMcpServer API v1");
        c.RoutePrefix = "swagger";
    });
}

app.Run();
 




