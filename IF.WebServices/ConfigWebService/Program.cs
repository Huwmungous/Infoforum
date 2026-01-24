using ConfigWebService.Data;
using ConfigWebService.Repositories;
using ConfigWebService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using IFGlobal;
using IFGlobal.Logging;
using IFGlobal.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});
var logger = bootstrapLoggerFactory.CreateLogger("ConfigWebService");

logger.LogDebug("ConfigWebService starting.");

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
#if DEBUG
    .AddJsonFile("appsettings.Debug.json", optional: true, reloadOnChange: true)
#endif
    .AddEnvironmentVariables();

var keycloakUrl = builder.Configuration["OidcConfig:Authority"]
    ?? throw new InvalidOperationException("OidcConfig:Authority not configured in appsettings.json");

var realm = builder.Configuration["OidcConfig:Realm:Name"]
    ?? throw new InvalidOperationException("OidcConfig:Realm:Name not configured in appsettings.json");

var clientId = builder.Configuration["OidcConfig:Realm:service:ClientId"]
    ?? throw new InvalidOperationException("OidcConfig:Realm:service:ClientId not configured in appsettings.json");

logger.LogDebug("KeycloakUrl: {KeycloakUrl}", keycloakUrl);
logger.LogDebug("Realm: {Realm}", realm);

// Use PortResolver for consistent port assignment
int port = PortResolver.GetPort();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port);
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{keycloakUrl}/auth/realms/{realm}";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<ConfigDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ConfigDatabase")));

builder.Services.AddScoped<ConfigRepository>();
builder.Services.AddScoped<ConfigService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Config Web Service",
        Version = "v1",
        Description = "Centralised configuration service for IF microservices"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ============================================================================
// DIRECT LOGGING SERVICES - Write directly to log DB, notify LoggerWebService
// ============================================================================
var logConnectionString = builder.Configuration.GetConnectionString("LogDatabase")
    ?? throw new InvalidOperationException("ConnectionStrings:LogDatabase not configured");
var loggerServiceUrl = builder.Configuration["LoggerService"];
var openIdConfig = $"{keycloakUrl}/auth/realms/{realm}";

// Parse connection string to PGConnectionConfig
var connParts = logConnectionString.Split(';')
    .Select(p => p.Split('=', 2))
    .Where(p => p.Length == 2)
    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

var logDbConfig = new PGConnectionConfig
{
    Host = connParts.GetValueOrDefault("Host") ?? throw new InvalidOperationException("LogDatabase connection string missing Host"),
    Port = int.TryParse(connParts.GetValueOrDefault("Port"), out var p) ? p : 5432,
    Database = connParts.GetValueOrDefault("Database") ?? throw new InvalidOperationException("LogDatabase connection string missing Database"),
    UserName = connParts.GetValueOrDefault("Username") ?? throw new InvalidOperationException("LogDatabase connection string missing Username"),
    Password = connParts.GetValueOrDefault("Password") ?? throw new InvalidOperationException("LogDatabase connection string missing Password")
};

// Create services manually so they can be shared with logger provider
var logEntryService = new LogEntryService(logDbConfig, bootstrapLoggerFactory.CreateLogger<LogEntryService>());
var httpClient = new HttpClient();
var directTryLogService = new DirectTryLogService(
    logEntryService,
    httpClient,
    openIdConfig,
    clientId,
    loggerServiceUrl,
    bootstrapLoggerFactory.CreateLogger<DirectTryLogService>());

builder.Services.AddSingleton(logDbConfig);
builder.Services.AddSingleton(logEntryService);
builder.Services.AddSingleton(directTryLogService);
builder.Services.AddHttpClient();

// Add DirectTryLogger to logging pipeline (writes to DB + console)
var minLogLevel = builder.Configuration.GetValue("Logging:LogLevel:Default", LogLevel.Information);
builder.Logging.AddProvider(new DirectTryLoggerProvider(
    () => directTryLogService,
    realm,
    clientId,
    builder.Environment.ApplicationName,
    builder.Environment.EnvironmentName,
    minLogLevel));

var app = builder.Build();

// Enable Swagger for all builds except UAT and PRD
#if UAT || PRD
// Swagger disabled in UAT/PRD builds
#else
app.UseSwagger();
app.UseSwaggerUI();
#endif

// Use permissive CORS for development builds
#if UAT || PRD
app.UseCors();
#else
app.UseCors("AllowAll");
#endif

app.UseAuthentication();
app.UseAuthorization();

app.UseRouting();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

logger.LogInformation("ConfigWebService starting on port {Port}", port);
logger.LogDebug("OIDC Authority: {Authority}", $"{keycloakUrl}/auth/realms/{realm}");

app.Run();
