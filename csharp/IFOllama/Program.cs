using IFGlobal;
using IFOllama;
using IFOllama.Controllers;
using IFOllama.RAG;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

int port = PortResolver.GetPort("IFOllama");
var builder = WebApplication.CreateBuilder(args);

// Add environment-specific configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Listen on configured port
builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));

// Fix for CS1729, CS0119, CS1525, CS1003, CS0103

// Corrected the instantiation of ConversationContextManager by ensuring the _logger is properly resolved from the service provider
builder.Services.AddSingleton<IConversationContextManager>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ConversationContextManager>>(); // Correctly resolve the _logger
    var cm = new ConversationContextManager(logger); // Pass the _logger to the constructor
    cm.Initialize();
    return cm;
});

// Fully-local embedding service (ONNX Sentence Transformer)
builder.Services.AddSingleton<IEmbeddingService, TextEmbeddingService>();

// RAG service using local embeddings
// builder.Services.AddScoped<IRagService, RagService>();

//builder.Services.AddSingleton<CodeContextService>(provider =>
//    new CodeContextService(
//        provider.GetRequiredService<IEmbeddingService>(),
//        provider.GetRequiredService<IConfiguration>(),
//        provider.GetRequiredService<ILogger<CodeContextService>>()));


// HTTP for Ollama and Context7 fetch
builder.Services.AddHttpClient();

// Controllers and Swagger
builder.Services.AddControllers();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.TypeInfoResolver = AppJsonContext.Default;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intelligence API", Version = "v1" });

    // Add JWT support to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.\n\nEnter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


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
        .WithOrigins(
          "https://longmanrd.net", 
          $"http://localhost:{port}",
          $"http://thehybrid:{port}", 
          $"http://gambit:{port}", 
          "http://localhost:4200")
        .AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowedToAllowWildcardSubdomains()
    )
);

var app = builder.Build();

// Trigger CodeContextService initialization
using (var scope = app.Services.CreateScope())
{
    // var codeContextService = scope.ServiceProvider.GetRequiredService<CodeContextService>();
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
