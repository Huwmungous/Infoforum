using CodeFormatterMcpServer.Protocol;
using CodeFormatterMcpServer.Services; 
using SfD.Global; using SfD.Global.Logging; 
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SfdLogger>(sp =>
{
    var config = new SfdLoggerConfiguration
    {
        MinimumLogLevel = LogLevel.Information,
        EnableRemoteLogging = true // or false, as needed
    };
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>(); 
    return new SfdLogger("CodeFormatterMcpServer", config.LoggerServiceUrl, config.ClientId, config.Realm);
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

// CRITICAL: Use centralized port management via SfD.Global
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register services
builder.Services.AddScoped<CodeFormatterService>();
builder.Services.AddScoped<McpServer>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeFormatterMcpServer v1");
    c.RoutePrefix = "swagger"; // UI at /swagger
});

// Health check endpoint
app.MapGet("/health", () =>
    Results.Ok(new { status = "healthy", service = "code-formatter-mcp", port, version = "1.0.0" }))
   .WithName("GetHealth")
   .WithTags("System")
   .Produces(StatusCodes.Status200OK, contentType: "application/json");

app.MapMethods("/", new[] { "GET", "HEAD" }, () => Results.Redirect("/health"));

app.MapGet("/toolslist", (IServiceProvider services) =>
{
    var mcpServer = services.GetRequiredService<McpServer>();
    var tools = mcpServer.GetAvailableTools();
    return Results.Ok(tools);
})
.WithName("GetAvailableTools")
.WithTags("Tools")
.Produces(StatusCodes.Status200OK, contentType: "application/json");

// MCP protocol endpoint
app.MapPost("/mcp", async context =>
{
    var mcpServer = context.RequestServices.GetRequiredService<McpServer>();

    using var reader = new StreamReader(context.Request.Body);
    var requestJson = await reader.ReadToEndAsync();

    app.Logger.LogInformation("Received MCP request: {Request}", requestJson);

    var responseJson = await mcpServer.HandleRequestStringAsync(requestJson);

    app.Logger.LogInformation("Sending MCP response: {Response}", responseJson);

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(responseJson);
});

app.Logger.LogInformation("Code Formatter MCP Server listening on port {Port}", port);
app.Logger.LogInformation("Endpoints: GET /health, POST /mcp");

app.Run();