using IFGlobal;
using IFOllama;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

int port = PortResolver.GetPort("IFOllama");
var builder = WebApplication.CreateBuilder(args);

// Add environment-specific configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Listen on configured port
builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));

// Fix for CS1729, CS0119, CS1525, CS1003, CS0103

// Corrected the instantiation of ConversationContextManager by ensuring the logger is properly resolved from the service provider
builder.Services.AddSingleton<IConversationContextManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ConversationContextManager>>(); // Correctly resolve the logger
    var cm = new ConversationContextManager(logger); // Pass the logger to the constructor
    cm.Initialize();
    return cm;
});

// Fully-local embedding service (ONNX Sentence Transformer)
builder.Services.AddSingleton<IEmbeddingService, TextEmbeddingService>();

// RAG service using local embeddings
builder.Services.AddScoped<IRagService, RagService>();

builder.Services.AddSingleton<CodeContextService>(sp =>
    new CodeContextService(
        sp.GetRequiredService<IEmbeddingService>(),
        builder.Configuration,
        sp.GetRequiredService<ILogger<CodeContextService>>() // Added the required logger argument
    )
);

// HTTP for Ollama and Context7 fetch
builder.Services.AddHttpClient();

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intelligence API", Version = "v1" })
);

// JWT Authentication & Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = "https://longmanrd.net/auth/realms/LongmanRd";
        opts.Audience = "account";
        opts.RequireHttpsMetadata = true;
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MustBeIntelligenceUser", p => p.RequireClaim("kc_groups", "IntelligenceUsers"));

// CORS
builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowSpecificOrigins", pb => pb
        .WithOrigins("https://longmanrd.net", $"http://localhost:{port}",
                     $"http://thehybrid:{port}", $"http://gambit:{port}", "http://localhost:4200")
        .AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowedToAllowWildcardSubdomains()
    )
);

var app = builder.Build();

// Trigger CodeContextService initialization
using (var scope = app.Services.CreateScope())
{
    var codeContextService = scope.ServiceProvider.GetRequiredService<CodeContextService>();
    // The constructor of CodeContextService already calls RebuildIndex
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama API V1"));
app.MapControllers();
app.Run();
