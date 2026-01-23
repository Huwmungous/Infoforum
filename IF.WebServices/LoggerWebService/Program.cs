using LoggerWebService.Hubs;
using IFGlobal.Logging;
using IFGlobal.WebServices;

var app = await ServiceFactory.CreateWithPostgresAsync(
    serviceName: "LoggerWebService",
    databaseConfigName: "loggerdb",
    description: "Centralised logging service",
    useIFLogger: false,
    useSignalR: true,
    configureServices: (services, context) =>
    {
        services.AddScoped<LogEntryService>();
    },
    configurePipeline: (app, context) =>
    {
        app.MapHub<LogHub>("/loghub");
    }
);

app.Run();
