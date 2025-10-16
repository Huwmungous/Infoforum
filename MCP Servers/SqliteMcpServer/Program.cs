using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqliteMcpServer.Protocol;
using SqliteMcpServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Existing service registrations
services.AddSingleton<SqliteService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteService>>();
            return new SqliteService(logger, dbPath);
builder.Services.AddSingleton<McpServer>();

var app = builder.Build();

var sseHub = new SseHub();

app.MapGet("/", () => Results.Json(new { ok = true, server = "SqliteMcpServer" }));
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

app.Run();

public sealed class SseHub
{
    private readonly Channel<string> _ch = Channel.CreateUnbounded<string>();
    public ChannelReader<string> Reader => _ch.Reader;
    public Task PushAsync(string evt, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var chunk = $"event: {evt}\n" + $"data: {json}\n\n";
        return _ch.Writer.WriteAsync(chunk).AsTask();
    }
}
