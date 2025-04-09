using IFGlobal;
using IFOllama;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;


int port = PortResolver.GetPort("IFOllama");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(port);
});

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

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("Authentication failed: " + context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});


// Configure authorization policies.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("MustBeIntelligenceUser", policy =>
    {
        policy.RequireClaim("kc_groups", "IntelligenceUsers");
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
