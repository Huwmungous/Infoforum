using SampleWebService.Repositories;
using IFGlobal.WebServices;

var app = await ServiceFactory.CreateWithPostgresAsync(
    serviceName: "SampleService",
    databaseConfigName: "postgresdb",
    description: "Service to act as pattern for further generation",
    configureServices: (services, context) =>
    {
        services.AddScoped<AccountRepository>();
    }
);

app.Run();
