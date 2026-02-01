using SampleWebService.Repositories;
using IFGlobal.WebServices;
using IFGlobal.Models;

// =============================================================================
// SAMPLE WEB SERVICE
// =============================================================================
// This service demonstrates best practices for IF microservices:
// 1. Using ServiceFactory for standardised bootstrap
// 2. Automatic ConfigService integration with caching  
// 3. Centralised IFLogger for remote logging
// 4. PostgreSQL database access via configuration
// =============================================================================

var app = await ServiceFactory.CreateWithPostgresAsync(
    serviceName: "SampleWebService",
    databaseConfigName: "rozebowl",            // Rozebowl database config from ConfigWebService
    description: "Sample service demonstrating IF infrastructure patterns",
    useIFLogger: true,
    configureServices: (services, context) =>
    {
        // PGConnectionConfig is automatically registered by ServiceFactory
        services.AddScoped<CoachRepository>();
    },
    configurePipeline: (app, context) =>
    {
        // Log database connection info at startup
        if (context.DatabaseConfig is PGConnectionConfig pgConfig)
        {
            context.Logger?.LogInformation(
                "SampleWebService connected to {Host}:{Port}/{Database}",
                pgConfig.Host,
                pgConfig.Port,
                pgConfig.Database);
        }
    }
);

app.Run();
