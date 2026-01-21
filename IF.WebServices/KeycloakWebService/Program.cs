using KeycloakWebService.Services;
using IFGlobal.WebServices;

var app = await ServiceFactory.CreateSimpleAsync(
    serviceName: "KeycloakWebService",
    description: "Keycloak administration and user management service",
    configureServices: (services, context) =>
    {
        services.AddScoped<KeycloakService>();
    }
);

app.Run();
