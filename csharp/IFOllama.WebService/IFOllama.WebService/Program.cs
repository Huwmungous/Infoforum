using IFGlobal;
using IFOllama.WebService.Data;
using IFOllama.WebService.Hubs;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add environment-specific configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// CRITICAL: Use centralised port management via IFGlobal.PortResolver
int port = PortResolver.GetPort();
builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));

// Register services
builder.Services.AddSingleton<IConversationStore, ConversationStore>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddHttpClient<McpRouterService>();
builder.Services.AddHttpClient<OllamaService>();

// Configure SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

// Configure controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IFOllama API",
        Version = "v1",
        Description = "AI chat service with MCP tool integration"
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add JWT security definition
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.\n\nEnter 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Authentication
var keycloakAuthority = builder.Configuration["Keycloak:Authority"] ?? "https://longmanrd.net/auth/realms/LongmanRd";
var keycloakAudience = builder.Configuration["Keycloak:Audience"] ?? "account";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = keycloakAuthority;
        opts.Audience = keycloakAudience;
        opts.RequireHttpsMetadata = true;

        // Configure for SignalR
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

// Configure Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MustBeIntelligenceUser", p =>
        p.RequireClaim("kc_groups", "IntelligenceUsers"));

// Configure CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
[
    "https://longmanrd.net",
    $"http://localhost:{port}",
    "http://localhost:3000",
    "http://localhost:4200"
];

builder.Services.AddCors(opts =>
    opts.AddPolicy("AllowSpecificOrigins", pb => pb
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowedToAllowWildcardSubdomains()));

// Configure file upload limits
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024; // 20MB
});

var app = builder.Build();

// Configure middleware pipeline
app.UseStaticFiles();
app.UseRouting();

// Disable buffering for streaming endpoints
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
    }
    await next();
});

app.UseCors("AllowSpecificOrigins");
app.UseAuthentication();
app.UseAuthorization();

// Configure Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama API V1");
    c.RoutePrefix = "swagger";
});

// Map endpoints
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();
