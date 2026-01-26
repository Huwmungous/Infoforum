using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IFGlobal.Auth;
using IFGlobal.Config;
using IFGlobal.Configuration;
using IFGlobal.Extensions;
using IFGlobal.Logging;
using IFGlobal.Models;

namespace IFGlobal.WebServices;

/// <summary>
/// Factory for creating standardised SfD web service applications.
/// Reduces boilerplate by handling common bootstrap patterns.
/// </summary>
public static partial class ServiceFactory
{
    /// <summary>
    /// Creates a fully configured WebApplication with standard SfD infrastructure.
    /// </summary>
    /// <param name="options">Configuration options for the service.</param>
    /// <returns>A configured WebApplication ready to run.</returns>
    public static async Task<WebApplication> CreateAsync(ServiceFactoryOptions options)
    {
        var builder = WebApplication.CreateBuilder();

        using var bootstrapLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Debug);
        });
        var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("ServiceFactory");

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        var serviceName = options.ServiceName;

#if DEBUG
        builder.Configuration.AddJsonFile("appsettings.Debug.json", optional: true, reloadOnChange: true);
        LogBuildConfiguration(bootstrapLogger, serviceName, "DEBUG");
#elif DEV
        LogBuildConfiguration(bootstrapLogger, serviceName, "DEV");
#elif SIT
        LogBuildConfiguration(bootstrapLogger, serviceName, "SIT");
#elif UAT
        LogBuildConfiguration(bootstrapLogger, serviceName, "UAT");
#elif PRD
        LogBuildConfiguration(bootstrapLogger, serviceName, "PRD");
#else
        LogBuildConfiguration(bootstrapLogger, serviceName, "Release/Deployed");
#endif

        // ============================================================================
        // BOOTSTRAP VIA CONFIGWEBSERVICE
        // ============================================================================
        // Configuration from appsettings.json (IF: section)
        var configServiceUrl = GetConfigValue(builder, "IF:ConfigService");
        var realm = GetConfigValue(builder, "IF:Realm");
        var client = GetConfigValue(builder, "IF:Client");
        
        // ClientSecret is optional - only needed for Service AppType
        var clientSecret = GetOptionalConfigValue(builder, "IF:ClientSecret", "IF_CLIENTSECRET");

        LogConfigServiceUrl(bootstrapLogger, serviceName, configServiceUrl);
        LogRealmAndClient(bootstrapLogger, serviceName, realm, client);

        var authTypeString = options.AuthType.ToString();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{SfdConfiguration.SectionName}:ConfigService"] = configServiceUrl,
            [$"{SfdConfiguration.SectionName}:Client"] = client,
            [$"{SfdConfiguration.SectionName}:Realm"] = realm,
            [$"{SfdConfiguration.SectionName}:AppType"] = authTypeString,
            [$"{SfdConfiguration.SectionName}:ClientSecret"] = clientSecret
        });

        builder.Services.Configure<SfdConfiguration>(
            builder.Configuration.GetSection(SfdConfiguration.SectionName));
        builder.Services.AddHttpClient();

        var configService = await BootstrapConfigServiceAsync(builder);

        LogConfigServiceInitialised(bootstrapLogger, serviceName, configService.ClientId, configService.Realm, authTypeString);

        LogAuthenticating(bootstrapLogger, serviceName);
        var accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync(configService);
        LogAuthenticatedSuccessfully(bootstrapLogger, serviceName);

        // Fire-and-forget remote bootstrap log
        _ = LogBootstrapToRemoteAsync(
            configService.LoggerService,
            serviceName,
            configService.Realm,
            configService.ClientId,
            accessToken,
            bootstrapLogger);

        // Database config from ConfigService
        object? databaseConfig = null;
        if (!string.IsNullOrEmpty(options.DatabaseConfigName) && options.DatabaseConfigType != null)
        {
            var configName = options.DatabaseConfigName;
            LogFetchingConfig(bootstrapLogger, serviceName, configName);

            var method = typeof(IConfigService).GetMethod(nameof(IConfigService.GetConfigAsync));
            var genericMethod = method!.MakeGenericMethod(options.DatabaseConfigType);

            var task = (Task)genericMethod.Invoke(configService, [options.DatabaseConfigName, accessToken, CancellationToken.None])!;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            databaseConfig = resultProperty!.GetValue(task);

            if (databaseConfig == null)
            {
                throw new InvalidOperationException($"Failed to fetch {options.DatabaseConfigName} configuration");
            }

            LogDatabaseConfig(bootstrapLogger, serviceName, databaseConfig);

            builder.Services.AddSingleton(options.DatabaseConfigType, databaseConfig);
        }

        // ============================================================================
        // PORT CONFIGURATION
        // ============================================================================
        int port = PortResolver.GetPort();
        builder.WebHost.ConfigureKestrel(opts => opts.ListenAnyIP(port));

        // ============================================================================
        // LOGGING CONFIGURATION
        // ============================================================================
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        SfdLogger? sfdLogger = null;
        if (options.UseIFLogger)
        {
#if DEBUG
            var loggerServiceUrl = builder.Configuration["ServiceUrls:LoggerService"]
                ?? configService.LoggerService
                ?? throw new InvalidOperationException("LoggerService URL not configured");
#else
            var loggerServiceUrl = configService.LoggerService
                ?? throw new InvalidOperationException("LoggerService URL not configured");
#endif

            var minLogLevel = builder.Configuration.GetValue("Logging:LogLevel:Default", LogLevel.Information);
            var sfdLoggerConfig = new SfdLoggerConfiguration
            {
                LoggerServiceUrl = loggerServiceUrl,
                ClientId = configService.ClientId,
                Realm = configService.Realm,
                MinimumLogLevel = minLogLevel,
                ApplicationName = serviceName,
                EnvironmentName = builder.Environment.EnvironmentName
            };
            builder.Logging.AddSfdLogger(sfdLoggerConfig);

            sfdLogger = new SfdLogger(
                serviceName,
                sfdLoggerConfig.LoggerServiceUrl,
                sfdLoggerConfig.ClientId,
                sfdLoggerConfig.Realm,
                serviceName,
                builder.Environment.EnvironmentName
            );
            builder.Services.AddSingleton(sfdLogger);
        }

        // ============================================================================
        // SERVICE REGISTRATION
        // ============================================================================
        builder.Services.AddSingleton<IConfigService>(configService);
        builder.Services.AddSingleton<IConfigProvider>(new ConfigProvider
        {
            ClientId = configService.ClientId,
            OpenIdConfig = configService.OpenIdConfig,
            LoggerService = configService.LoggerService,
            Realm = configService.Realm
        });

        // Create context for callbacks
        var context = new ServiceFactoryContext
        {
            ConfigService = configService,
            Configuration = builder.Configuration,
            AccessToken = accessToken,
            DatabaseConfig = databaseConfig,
            Logger = sfdLogger,
            Port = port
        };

        // ============================================================================
        // AUTHENTICATION CONFIGURATION
        // ============================================================================
        if (options.UseAuthentication)
        {
            builder.Services.AddAuthentication(authOptions =>
            {
                authOptions.DefaultAuthenticateScheme = "Bearer";
                authOptions.DefaultChallengeScheme = "Bearer";
            })
            .AddJwtBearer("Bearer", jwtOptions =>
            {
                jwtOptions.Authority = configService.OpenIdConfig;
#if DEBUG
                jwtOptions.RequireHttpsMetadata = false;
#else
                jwtOptions.RequireHttpsMetadata = true;
#endif
                jwtOptions.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudiences = ["account", configService.ClientId],
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

            builder.Services.AddAuthorization();
        }

        // Allow service-specific registrations
        options.ConfigureServices?.Invoke(builder.Services, context);
        if (options.ConfigureServicesAsync != null)
        {
            await options.ConfigureServicesAsync(builder.Services, context);
        }

        // ============================================================================
        // MVC AND API CONFIGURATION
        // ============================================================================
        builder.Services.AddSfdHealthController(serviceName);
        builder.Services.AddControllers()
            .AddSfdCommonControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new()
            {
                Title = serviceName,
                Version = "v1",
                Description = options.Description ?? $"{serviceName} API"
            });

            // Add Bearer token security definition when authentication is enabled
            if (options.UseAuthentication)
            {
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter: Bearer {your JWT token}",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey
                });
                
                // Add operation filter to apply security to [Authorize] endpoints
                c.OperationFilter<IFGlobal.Swagger.JwtSecurityOperationFilter>();
            }

            options.ConfigureSwagger?.Invoke(c);
        });

        if (options.UseSignalR)
        {
            builder.Services.AddSignalR();
        }

        builder.Services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy("AllowAll", policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // ============================================================================
        // HTTP REQUEST PIPELINE
        // ============================================================================

        // Apply PathBase for reverse proxy deployments (non-DEBUG only)
#if !DEBUG
        if (!string.IsNullOrEmpty(options.PathBase))
        {
            var pathBase = options.PathBase;
            app.UsePathBase(pathBase);
            LogUsingPathBase(bootstrapLogger, serviceName, pathBase);
        }
#endif

        // Enable Swagger for all builds except UAT and PRD
#if UAT || PRD
        // Swagger disabled in UAT/PRD builds
#else
        app.UseSwagger();
        app.UseSwaggerUI(uiOptions =>
        {
            // Add auth persistence when authentication is enabled
            if (options.UseAuthentication)
            {
                uiOptions.EnablePersistAuthorization();
                uiOptions.UseRequestInterceptor("(req) => { const auth = window.ui?.authSelectors?.authorized(); if (auth) { const bearerAuth = auth.get('Bearer'); if (bearerAuth) { const token = bearerAuth.get('value'); if (token) { req.headers.Authorization = token; } } } return req; }");
            }
            options.ConfigureSwaggerUI?.Invoke(uiOptions);
        });
#endif

        // Use permissive CORS for development builds, standard CORS for production
#if UAT || PRD
        app.UseCors();
#else
        app.UseCors("AllowAll");
#endif

        if (options.UseAuthentication)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }
        else
        {
            app.UseAuthorization();
        }

        options.ConfigurePipeline?.Invoke(app, context);

        app.MapControllers();

        LogServiceStarting(bootstrapLogger, serviceName, port);
        LogConfigServiceDetails(bootstrapLogger, configService.ClientId, configService.Realm);

        return app;
    }

    /// <summary>
    /// Gets a configuration value from appsettings, with optional env var fallback.
    /// </summary>
    private static string GetConfigValue(WebApplicationBuilder builder, string configKey, string? envVarName = null)
    {
        // Always prefer appsettings.json
        var value = builder.Configuration[configKey];
        if (!string.IsNullOrEmpty(value))
            return value;

        // Fallback to environment variable if provided
        if (!string.IsNullOrEmpty(envVarName))
        {
            value = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        throw new InvalidOperationException(
            envVarName != null
                ? $"Configuration '{configKey}' not found in appsettings and '{envVarName}' environment variable not set"
                : $"Configuration '{configKey}' not found in appsettings");
    }

    private static string? GetOptionalConfigValue(WebApplicationBuilder builder, string configKey, string? envVarName = null)
    {
        // Always prefer appsettings.json
        var value = builder.Configuration[configKey];
        if (!string.IsNullOrEmpty(value))
            return value;

        // Fallback to environment variable if provided
        if (!string.IsNullOrEmpty(envVarName))
        {
            value = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Bootstraps the ConfigService using a temporary service provider.
    /// </summary>
    private static async Task<ConfigService> BootstrapConfigServiceAsync(WebApplicationBuilder builder)
    {
        var tempServices = new ServiceCollection();
        tempServices.Configure<SfdConfiguration>(
            builder.Configuration.GetSection(SfdConfiguration.SectionName));
        tempServices.AddHttpClient();
        tempServices.AddLogging();

        using var tempProvider = tempServices.BuildServiceProvider();

        var sfdOptions = tempProvider.GetRequiredService<IOptions<SfdConfiguration>>();
        var httpClientFactory = tempProvider.GetRequiredService<IHttpClientFactory>();
        var configServiceLogger = tempProvider.GetRequiredService<ILogger<ConfigService>>();

        var configService = new ConfigService(sfdOptions, httpClientFactory, configServiceLogger);
        await configService.InitializeAsync();

        return configService;
    }

    /// <summary>
    /// Logs database configuration info based on the config type.
    /// </summary>
    private static void LogDatabaseConfig(ILogger logger, string serviceName, object config)
    {
        switch (config)
        {
            case PGConnectionConfig pg:
                LogDatabaseConnection(logger, serviceName, pg.Host, pg.Port, pg.Database);
                break;
            default:
                var configType = config.GetType().Name;
                LogDatabaseConfigLoaded(logger, serviceName, configType);
                break;
        }
    }

    /// <summary>
    /// Creates a service with Firebird database configuration.
    /// </summary>
    [Obsolete("Firebird support is deprecated. Use CreateWithPostgresAsync instead.")]
    public static Task<WebApplication> CreateWithFirebirdAsync(
        string serviceName,
        string? description = null,
        bool useAuthentication = true,
        bool useSfdLogger = true,
        string? pathBase = null,
        AuthType authType = AuthType.User,
        Action<IServiceCollection, ServiceFactoryContext>? configureServices = null,
        Action<WebApplication, ServiceFactoryContext>? configurePipeline = null)
    {
        return CreateAsync(new ServiceFactoryOptions
        {
            ServiceName = serviceName,
            Description = description,
            DatabaseConfigName = "firebirddb",
            DatabaseConfigType = typeof(FBConnectionConfig),
            UseAuthentication = useAuthentication,
            UseIFLogger = useSfdLogger,
            PathBase = pathBase,
            AuthType = authType,
            ConfigureServices = configureServices,
            ConfigurePipeline = configurePipeline
        });
    }

    /// <summary>
    /// Creates a service with PostgreSQL database configuration.
    /// </summary>
    public static Task<WebApplication> CreateWithPostgresAsync(
        string serviceName,
        string databaseConfigName,
        string? description = null,
        bool useAuthentication = true,
        bool useIFLogger = true,
        bool useSignalR = false,
        string? pathBase = null,
        Action<IServiceCollection, ServiceFactoryContext>? configureServices = null,
        Action<WebApplication, ServiceFactoryContext>? configurePipeline = null)
    {
        return CreateAsync(new ServiceFactoryOptions
        {
            ServiceName = serviceName,
            Description = description,
            DatabaseConfigName = databaseConfigName,
            DatabaseConfigType = typeof(PGConnectionConfig),
            UseAuthentication = useAuthentication,
            UseIFLogger = useIFLogger,
            UseSignalR = useSignalR,
            PathBase = pathBase,
            ConfigureServices = configureServices,
            ConfigurePipeline = configurePipeline
        });
    }

    /// <summary>
    /// Creates a simple service without database configuration.
    /// </summary>
    public static Task<WebApplication> CreateSimpleAsync(
        string serviceName,
        string? description = null,
        bool useAuthentication = true,
        bool useSfdLogger = true,
        bool useSignalR = false,
        string? pathBase = null,
        Action<IServiceCollection, ServiceFactoryContext>? configureServices = null,
        Action<WebApplication, ServiceFactoryContext>? configurePipeline = null)
    {
        return CreateAsync(new ServiceFactoryOptions
        {
            ServiceName = serviceName,
            Description = description,
            UseAuthentication = useAuthentication,
            UseIFLogger = useSfdLogger,
            UseSignalR = useSignalR,
            PathBase = pathBase,
            ConfigureServices = configureServices,
            ConfigurePipeline = configurePipeline
        });
    }

    /// <summary>
    /// Sends a bootstrap log entry to the remote LoggerWebService.
    /// Fire-and-forget - does not block startup if logger is unavailable.
    /// </summary>
    private static async Task LogBootstrapToRemoteAsync(
        string? loggerServiceUrl,
        string serviceName,
        string realm,
        string clientId,
        string accessToken,
        ILogger bootstrapLogger)
    {
        if (string.IsNullOrEmpty(loggerServiceUrl))
        {
            LogSkippingRemoteBootstrap(bootstrapLogger, serviceName);
            return;
        }

        try
        {
            var buildConfig =
#if DEBUG
                "DEBUG";
#elif DEV
                "DEV";
#elif SIT
                "SIT";
#elif UAT
                "UAT";
#elif PRD
                "PRD";
#else
                "Release";
#endif

            var port = PortResolver.GetPort();
            var hostname = Environment.MachineName;
            var text = string.Concat(serviceName, " starting on ", hostname, ":", port.ToString(), " (", buildConfig, ")");

            var logEntry = new
            {
                realm,
                client = clientId,
                logData = new
                {
                    level = "Information",
                    category = "Bootstrap",
                    message = "ServiceBootstrap",
                    text,
                    serviceName,
                    port,
                    hostname,
                    buildConfiguration = buildConfig,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            };

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Don't wait too long
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var json = System.Text.Json.JsonSerializer.Serialize(logEntry);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(string.Concat(loggerServiceUrl, "/api/logs"), content);

            if (response.IsSuccessStatusCode)
            {
                LogBootstrapSent(bootstrapLogger, serviceName);
            }
            else
            {
                LogBootstrapFailed(bootstrapLogger, serviceName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Fire and forget - don't crash startup if logger is unavailable
            LogBootstrapError(bootstrapLogger, serviceName, ex.Message);
        }
    }

    // ============================================================================
    // HIGH-PERFORMANCE LOG METHODS (Source Generated)
    // ============================================================================

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Build Configuration: {BuildConfig}")]
    private static partial void LogBuildConfiguration(ILogger logger, string serviceName, string buildConfig);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: ConfigService URL: {ConfigServiceUrl}")]
    private static partial void LogConfigServiceUrl(ILogger logger, string serviceName, string configServiceUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Realm={Realm}, Client={Client}")]
    private static partial void LogRealmAndClient(ILogger logger, string serviceName, string realm, string client);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: ConfigService initialised. ClientId={ClientId}, Realm={Realm}, AppType={AppType}")]
    private static partial void LogConfigServiceInitialised(ILogger logger, string serviceName, string clientId, string realm, string appType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Authenticating service account...")]
    private static partial void LogAuthenticating(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Service authenticated successfully")]
    private static partial void LogAuthenticatedSuccessfully(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Fetching {ConfigName} configuration...")]
    private static partial void LogFetchingConfig(ILogger logger, string serviceName, string configName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Database: {Host}:{Port}/{Database}")]
    private static partial void LogDatabaseConnection(ILogger logger, string serviceName, string host, int port, string database);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Database config loaded: {ConfigType}")]
    private static partial void LogDatabaseConfigLoaded(ILogger logger, string serviceName, string configType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Using PathBase: {PathBase}")]
    private static partial void LogUsingPathBase(ILogger logger, string serviceName, string pathBase);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} starting on port {Port}")]
    private static partial void LogServiceStarting(ILogger logger, string serviceName, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "ConfigService: ClientId={ClientId}, Realm={Realm}")]
    private static partial void LogConfigServiceDetails(ILogger logger, string clientId, string realm);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Skipping remote bootstrap log - LoggerService URL not configured")]
    private static partial void LogSkippingRemoteBootstrap(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Bootstrap log sent to LoggerService")]
    private static partial void LogBootstrapSent(ILogger logger, string serviceName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Bootstrap log failed: {StatusCode}")]
    private static partial void LogBootstrapFailed(ILogger logger, string serviceName, System.Net.HttpStatusCode statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ServiceName}: Could not send bootstrap log: {Error}")]
    private static partial void LogBootstrapError(ILogger logger, string serviceName, string error);
}