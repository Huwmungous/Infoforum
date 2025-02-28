using IFGlobal;
using IFOllama;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;


int port = PortResolver.GetPort("IFOllama");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Listen on any IP on the specified port.
    serverOptions.ListenAnyIP(port);
});

// Add services to the container.
builder.Services.AddSingleton<IConversationContextManager>(serviceProvider =>
{
    var conversationManager = new ConversationContextManager();
    conversationManager.Initialize();
    return conversationManager;
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intelligence API", Version = "v1" });
});

// Existing code
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = "https://your-keycloak-domain/auth/realms/your-realm";
    options.Audience = "your-client-id";
    options.RequireHttpsMetadata = false; // Set to true in production.
});

// Configure authorization policies.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireIntelligenceUsersGroup", policy =>
    {
        policy.RequireClaim("groups", "IntelligenceUsers");
    });
});

// Add CORS services with a specific policy.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policyBuilder => policyBuilder
            .WithOrigins(
              "https://longmanrd.net",
             $"http://localhost:{port}",
             $"http://thehybrid:{port}",
             $"http://gambit:{port}",
              "http://localhost:4200")
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseStaticFiles();

app.UseRouting();

// Enable CORS before authentication and authorization.
app.UseCors("AllowSpecificOrigins");

// Add authentication and authorization middleware.
app.UseAuthentication();
app.UseAuthorization();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama API V1");
    c.RoutePrefix = "swagger"; // Swagger UI available at /swagger
});

app.MapControllers();

app.Run();
