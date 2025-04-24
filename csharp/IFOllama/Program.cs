using IFGlobal;
using IFOllama;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Net.Http;
using System.Net.Http.Headers;

int port = PortResolver.GetPort("IFOllama");

var builder = WebApplication.CreateBuilder(args);

// Listen on configured port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

// Conversation context manager
builder.Services.AddSingleton<IConversationContextManager>(sp =>
{
    var cm = new ConversationContextManager();
    cm.Initialize();
    return cm;
});

// MVC
builder.Services.AddControllers();

// Default HTTP client (for Ollama, Context7 retrieval, etc.)
builder.Services.AddHttpClient();

// OpenAI client for embeddings (RAG)
builder.Services.AddHttpClient("OpenAI", client =>
{
    client.BaseAddress = new Uri("https://api.openai.com");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", builder.Configuration["OpenAI:ApiKey"]);
});

// Register RAG service
builder.Services.AddSingleton<IRagService, RagService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intelligence API", Version = "v1" });
});

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = "https://longmanrd.net/auth/realms/LongmanRd";
    options.Audience = "account";
    options.RequireHttpsMetadata = true;
    options.Events.OnAuthenticationFailed = context =>
    {
        Console.WriteLine("Authentication failed: " + context.Exception.Message);
        return Task.CompletedTask;
    };
});

// Authorization
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("MustBeIntelligenceUser", policy =>
        policy.RequireClaim("kc_groups", "IntelligenceUsers")
    );
});

// CORS
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowSpecificOrigins", policy =>
        policy.WithOrigins(
            "https://longmanrd.net",
            $"http://localhost:{port}",
            $"http://thehybrid:{port}",
            $"http://gambit:{port}",
            "http://localhost:4200"
        )
        .SetIsOriginAllowedToAllowWildcardSubdomains()
        .AllowAnyHeader()
        .AllowAnyMethod()
    );
});

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// CORS → Auth
app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama API V1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
