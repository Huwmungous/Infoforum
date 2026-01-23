using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IFGlobal.Configuration;
using IFGlobal.Logging;

namespace IFGlobal.WebServices;

/// <summary>
/// Configuration options for ServiceFactory.
/// </summary>
public class ServiceFactoryOptions
{
    /// <summary>
    /// The name of the service (used for logging and health endpoint).
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Optional description for Swagger documentation.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The configuration name to fetch from ConfigService (e.g., "firebirddb", "loggerdb").
    /// If null, no database configuration is fetched.
    /// </summary>
    public string? DatabaseConfigName { get; set; }

    /// <summary>
    /// The type of database connection to fetch.
    /// Must be set if DatabaseConfigName is set.
    /// </summary>
    public Type? DatabaseConfigType { get; set; }

    /// <summary>
    /// Whether to configure SfdLogger for remote logging.
    /// Set to false for LoggerWebService to avoid circular dependency.
    /// Default is true.
    /// </summary>
    public bool UseIFLogger { get; set; } = true;

    /// <summary>
    /// Whether to add SignalR services.
    /// Default is false.
    /// </summary>
    public bool UseSignalR { get; set; } = false;

    /// <summary>
    /// Whether to add authentication/authorization middleware.
    /// Default is false.
    /// </summary>
    public bool UseAuthentication { get; set; } = true;

    /// <summary>
    /// The type of application for authentication purposes.
    /// Affects how the ClientId is resolved:
    /// - Service: Uses the standard service client (e.g., "dev-login-svc")
    /// - Patient: Uses the patient portal client (e.g., "dev-login-pps")
    /// - User: Uses the user client (e.g., "dev-login")
    /// Default is Service.
    /// </summary>
    public AuthType AuthType { get; set; } = AuthType.Service;

    /// <summary>
    /// The base path for the service when deployed behind a reverse proxy.
    /// For example, "/pps" if the service is accessed via https://example.com/pps/
    /// Only applied in non-DEBUG builds.
    /// </summary>
    public string? PathBase { get; set; }

    // ============================================================================
    // STATIC CONFIGURATION (Decoupled Mode)
    // When UseStaticConfig=true, the service bypasses ConfigWebService and uses
    // the static values below. This is intended for services that must start
    // independently (e.g., LoggerWebService when Config is unavailable).
    // ============================================================================

    /// <summary>
    /// When true, bypasses ConfigWebService and uses static configuration.
    /// Requires StaticOpenIdConfig, StaticClientId, and StaticRealm to be set.
    /// Default is false (uses ConfigWebService bootstrap).
    /// <para>
    /// <b>Validation (Commit 2):</b> ServiceFactory.CreateAsync must throw a clear
    /// startup exception if UseStaticConfig=true but required fields are null.
    /// </para>
    /// </summary>
    public bool UseStaticConfig { get; set; } = false;

    /// <summary>
    /// The OpenID Connect authority URL for JWT validation.
    /// Example: "https://sfddevelopment.com/auth/realms/SfdDevelopment_Dev"
    /// Only used when UseStaticConfig is true.
    /// </summary>
    public string? StaticOpenIdConfig { get; set; }

    /// <summary>
    /// The client ID for JWT audience validation (e.g., "dev-login-svc").
    /// Only used when UseStaticConfig is true.
    /// </summary>
    public string? StaticClientId { get; set; }

    /// <summary>
    /// The realm name for logging context (e.g., "SfdDevelopment_Dev").
    /// Only used when UseStaticConfig is true.
    /// </summary>
    public string? StaticRealm { get; set; }

    /// <summary>
    /// The database connection string for services that bypass ConfigWebService.
    /// Only used when UseStaticConfig is true and DatabaseConfigName is set.
    /// <para>
    /// <b>Important:</b> This property exists solely for decoupled-mode services
    /// (e.g., LoggerWebService). Do not use this for services that can rely on
    /// ConfigWebService—use the standard DatabaseConfigName approach instead.
    /// </para>
    /// </summary>
    public string? StaticDatabaseConnectionString { get; set; }

    /// <summary>
    /// Callback to configure additional services after standard configuration.
    /// Use this for synchronous service registration.
    /// </summary>
    public Action<IServiceCollection, ServiceFactoryContext>? ConfigureServices { get; set; }

    /// <summary>
    /// Async callback to configure additional services after standard configuration.
    /// Use this when you need to fetch additional config from ConfigService.
    /// </summary>
    public Func<IServiceCollection, ServiceFactoryContext, Task>? ConfigureServicesAsync { get; set; }

    /// <summary>
    /// Callback to configure the HTTP pipeline after standard middleware.
    /// </summary>
    public Action<WebApplication, ServiceFactoryContext>? ConfigurePipeline { get; set; }

    /// <summary>
    /// Callback to configure Swagger options.
    /// </summary>
    public Action<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>? ConfigureSwagger { get; set; }

    /// <summary>
    /// Callback to configure Swagger UI options.
    /// </summary>
    public Action<Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIOptions>? ConfigureSwaggerUI { get; set; }
}

/// <summary>
/// Context passed to configuration callbacks containing resolved services.
/// </summary>
public class ServiceFactoryContext
{
    /// <summary>
    /// The initialised ConfigService instance.
    /// </summary>
    public required IConfigService ConfigService { get; init; }

    /// <summary>
    /// The application configuration.
    /// </summary>
    public required IConfiguration Configuration { get; init; }

    /// <summary>
    /// The authenticated access token for fetching additional config.
    /// Null when UseStaticConfig=true (no Keycloak authentication at startup).
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// The database configuration object (if DatabaseConfigName was specified).
    /// Cast to FBConnectionConfig, PGConnectionConfig, etc. as needed.
    /// </summary>
    public object? DatabaseConfig { get; init; }

    /// <summary>
    /// The SfdLogger instance (if UseIFLogger is true).
    /// </summary>
    public SfdLogger? Logger { get; init; }

    /// <summary>
    /// The port the service is configured to listen on.
    /// </summary>
    public int Port { get; init; }
}