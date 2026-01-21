using CodeAnalysisMcpServer.Protocol;
using CodeAnalysisMcpServer.Tools;
using SfD.Global; using SfD.Global.Logging; 
using System.Reflection;
using System.Runtime;

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var asm = Assembly.GetExecutingAssembly();
    var xmlName = $"{asm.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// CRITICAL: Use centralized port management via SfD.Global
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(port); });

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// MEMORY OPTIMIZATION: Configure GC settings for server workload
GCSettings.LatencyMode = GCLatencyMode.Batch; // Better for throughput
Console.WriteLine($"GC Mode: {GCSettings.LatencyMode}, IsServerGC: {GCSettings.IsServerGC}");

// Register services - TRANSIENT for better memory cleanup
// Note: Transient services are created each time and disposed after use
builder.Services.AddTransient<DelphiSqlPatternExtractor>(sp =>
    new DelphiSqlPatternExtractor(sp.GetRequiredService<SfdLogger>()));

builder.Services.AddTransient<AntlrDelphiSqlExtractor>(sp =>
    new AntlrDelphiSqlExtractor(sp.GetRequiredService<SfdLogger>()));

builder.Services.AddTransient<AntlrDelphiCodeAnalyzer>(sp =>
    new AntlrDelphiCodeAnalyzer(sp.GetRequiredService<SfdLogger>()));

// builder.Services.AddTransient<UnifiedSqlExtractor>(sp =>
//     new UnifiedSqlExtractor(sp.GetRequiredService<SfdLogger>()));

// FIXED: Now injects DelphiSqlPatternExtractor (the working regex extractor)
builder.Services.AddTransient<CodeAnalysisTools>(sp =>
    new CodeAnalysisTools(
        sp.GetRequiredService<AntlrDelphiSqlExtractor>(),
        sp.GetRequiredService<DelphiSqlPatternExtractor>(),
        sp.GetRequiredService<AntlrDelphiCodeAnalyzer>(),
        sp.GetRequiredService<SfdLogger>()
    ));

builder.Services.AddScoped<McpServer>(sp =>
    new McpServer(
        sp.GetRequiredService<SfdLogger>(),
        sp.GetRequiredService<CodeAnalysisTools>()
    ));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeAnalysisMcpServer v1");
    c.RoutePrefix = "swagger"; // UI at /swagger
});

// Health check endpoint with memory stats
app.MapGet("/health", () =>
{
    var memoryInfo = GC.GetGCMemoryInfo();
    var gen0 = GC.CollectionCount(0);
    var gen1 = GC.CollectionCount(1);
    var gen2 = GC.CollectionCount(2);

    return Results.Ok(new
    {
        status = "healthy",
        service = "code-analysis-mcp",
        port,
        version = "2.1.1-memory-optimized",
        features = new[] { "regex-extraction", "antlr-sql-extraction", "antlr-code-analysis", "sql-deduplication", "memory-management" },
        memory = new
        {
            totalMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
            heapSizeMB = memoryInfo.HeapSizeBytes / 1024.0 / 1024.0,
            fragmentedMB = memoryInfo.FragmentedBytes / 1024.0 / 1024.0,
            gen0Collections = gen0,
            gen1Collections = gen1,
            gen2Collections = gen2,
            gcMode = GCSettings.LatencyMode.ToString(),
            isServerGC = GCSettings.IsServerGC
        }
    });
});

// Memory management endpoint - force GC collection
app.MapPost("/admin/gc", () =>
{
    var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    var freedMB = beforeMB - afterMB;

    return Results.Ok(new
    {
        status = "gc_completed",
        memoryBeforeMB = beforeMB,
        memoryAfterMB = afterMB,
        freedMB
    });
})
.WithName("ForceGarbageCollection")
.WithTags("Admin");

// Memory trim endpoint - aggressive memory reclaim
app.MapPost("/admin/trim", () =>
{
    var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    // Aggressive memory cleanup
    GC.Collect(2, GCCollectionMode.Aggressive, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Aggressive, true, true);

    // Compact Large Object Heap
    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
    GC.Collect();

    var afterMB = GC.GetTotalMemory(true) / 1024.0 / 1024.0;
    var freedMB = beforeMB - afterMB;

    return Results.Ok(new
    {
        status = "memory_trimmed",
        memoryBeforeMB = beforeMB,
        memoryAfterMB = afterMB,
        freedMB,
        lohCompacted = true
    });
})
.WithName("TrimMemory")
.WithTags("Admin");

app.MapMethods("/", ["GET", "HEAD"], () => Results.Redirect("/health"));

app.MapGet("/toolslist", (IServiceProvider services) =>
{
    var mcpServer = services.GetRequiredService<McpServer>();
    var tools = mcpServer.GetAvailableTools();
    return Results.Ok(tools);
})
.WithName("GetAvailableTools")
.WithTags("Tools")
.Produces(StatusCodes.Status200OK, contentType: "application/json");

// Test endpoint for comparing extraction methods
app.MapGet("/test/extract-comparison", async (
    string path,
    AntlrDelphiSqlExtractor antlrExtractor,
    DelphiSqlPatternExtractor regexExtractor) =>
{
    var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    var antlrResults = File.Exists(path)
        ? antlrExtractor.ExtractFromFile(path)
        : antlrExtractor.ExtractFromDirectory(path);

    var regexResults = File.Exists(path)
        ? regexExtractor.ExtractFromFile(path)
        : regexExtractor.ExtractFromDirectory(path);

    // Dispose extractors if IDisposable
    (antlrExtractor as IDisposable)?.Dispose();
    (regexExtractor as IDisposable)?.Dispose();

    var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    return Results.Ok(new
    {
        path,
        antlr = new
        {
            count = antlrResults.Count,
            statements = antlrResults.Take(5)  // First 5 for preview
        },
        regex = new
        {
            count = regexResults.Count,
            statements = regexResults.Take(5)
        },
        comparison = new
        {
            difference = Math.Abs(antlrResults.Count - regexResults.Count),
            antlrAdvantage = antlrResults.Count - regexResults.Count
        },
        memory = new
        {
            beforeMB,
            afterMB,
            usedMB = afterMB - beforeMB
        }
    });
})
.WithName("CompareExtractionMethods")
.WithTags("Testing");

// Test endpoint for code analysis features
app.MapGet("/test/code-analysis", async (
    string path,
    AntlrDelphiCodeAnalyzer codeAnalyzer) =>
{
    if (!File.Exists(path) && !Directory.Exists(path))
    {
        return Results.NotFound(new { error = "Path not found", path });
    }

    var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    var dbCalls = File.Exists(path)
        ? codeAnalyzer.FindDatabaseCalls(path)
        : Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories)
            .SelectMany(f => codeAnalyzer.FindDatabaseCalls(f))
            .ToList();

    var classes = File.Exists(path)
        ? codeAnalyzer.ExtractClassDefinitions(path)
        : Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories)
            .SelectMany(f => codeAnalyzer.ExtractClassDefinitions(f))
            .ToList();

    var methods = File.Exists(path)
        ? codeAnalyzer.ExtractMethodSignatures(path)
        : Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories)
            .SelectMany(f => codeAnalyzer.ExtractMethodSignatures(f))
            .ToList();

    // Dispose analyzer if IDisposable
    (codeAnalyzer as IDisposable)?.Dispose();

    var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

    return Results.Ok(new
    {
        path,
        summary = new
        {
            databaseCalls = dbCalls.Count,
            classes = classes.Count,
            methods = methods.Count
        },
        samples = new
        {
            firstDbCall = dbCalls.FirstOrDefault(),
            firstClass = classes.FirstOrDefault(),
            firstMethod = methods.FirstOrDefault()
        },
        memory = new
        {
            beforeMB,
            afterMB,
            usedMB = afterMB - beforeMB
        }
    });
})
.WithName("TestCodeAnalysis")
.WithTags("Testing");

// MCP protocol endpoint with memory tracking
app.MapPost("/rpc", async context =>
{
    var beforeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    var requestSize = context.Request.ContentLength ?? 0;

    var logger = context.RequestServices.GetRequiredService<SfdLogger>();
    var mcpServer = context.RequestServices.GetRequiredService<McpServer>();
    using var reader = new StreamReader(context.Request.Body);
    var requestJson = await reader.ReadToEndAsync();

    logger.LogInformation($"Received MCP request: {requestSize} bytes, Memory: {beforeMB:F2} MB");

    var responseJson = await mcpServer.HandleRequestStringAsync(requestJson);

    var afterMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    var memoryDelta = afterMB - beforeMB;

    if (memoryDelta > 10) // Log if memory increased by more than 10MB
    {
        logger.LogWarning($"High memory usage for request: {memoryDelta:F2} MB increase");
    }

    logger.LogInformation($"Sending MCP response, Memory: {afterMB:F2} MB (delta: {memoryDelta:F2} MB)");

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(responseJson);

    // Suggest GC if memory grew significantly
    if (memoryDelta > 50)
    {
        logger.LogInformation("Suggesting GC collection due to high memory delta");
        GC.Collect(1, GCCollectionMode.Optimized, false);
    }
});

var sfdLogger = app.Services.GetRequiredService<SfdLogger>();
sfdLogger.LogInformation($"Code Analysis MCP Server listening on port {port}");
sfdLogger.LogInformation("Features: ANTLR SQL extraction, ANTLR code analysis, SQL deduplication, Memory Management");
sfdLogger.LogInformation("Endpoints: GET /health, POST /rpc, GET /toolslist, POST /admin/gc, POST /admin/trim");
sfdLogger.LogInformation($"Memory: GC Mode={GCSettings.LatencyMode}, Server GC={GCSettings.IsServerGC}, Initial Memory={GC.GetTotalMemory(false) / 1024.0 / 1024.0:F2} MB");

app.Run();