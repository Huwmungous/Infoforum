using IFGlobal.WebServices;
using IFOllama.WebService.Data;
using IFOllama.WebService.Hubs;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Reflection;

var app = await ServiceFactory.CreateAsync(new ServiceFactoryOptions
{
    ServiceName = "IFOllama.WebService",
    Description = "AI Chat Service with Ollama and MCP Tool Integration",
    UseAuthentication = true,
    UseSfdLogger = true,
    UseSignalR = true,
    
    ConfigureServices = (services, context) =>
    {
        // Register application services
        services.AddSingleton<IConversationStore, ConversationStore>();
        services.AddSingleton<FileStorageService>();
        services.AddHttpClient<McpRouterService>();
        services.AddHttpClient<OllamaService>();
        
        // Configure file upload limits
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20MB
        });
        
        // Configure authorization policy for ChatHub
        services.AddAuthorization(options =>
        {
            options.AddPolicy("MustBeIntelligenceUser", policy =>
            {
                policy.RequireAuthenticatedUser();
                // Add additional requirements as needed, e.g.:
                // policy.RequireClaim("realm_access", "intelligence-user");
            });
        });
    },
    
    ConfigureSwagger = swaggerOptions =>
    {
        var asm = Assembly.GetExecutingAssembly();
        var xmlName = $"{asm.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlName);
        if (File.Exists(xmlPath))
        {
            swaggerOptions.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    },
    
    ConfigureSwaggerUI = uiOptions =>
    {
        uiOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama.WebService v1");
        uiOptions.RoutePrefix = "swagger";
    },
    
    ConfigurePipeline = (application, context) =>
    {
        // Map SignalR hub
        application.MapHub<ChatHub>("/chathub");
    }
});

app.Run();
