using IFGlobal;
using IFOllama;
using Microsoft.OpenApi.Models;

int port = PortResolver.GetPort("IFOllama");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // serverOptions.ListenLocalhost(port); 
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
builder.Services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "Intelligence API", Version = "v1" }); });

// Add CORS services with a specific policy
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

app.UseStaticFiles(); // Serve static files

app.UseCors("AllowSpecificOrigins"); // Enable CORS with the specified policy

app.UseRouting(); // This should be explicitly added if it’s not already there

app.UseSwagger(); // Enable middleware to serve generated Swagger as a JSON endpoint
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFOllama API V1");
    c.RoutePrefix = "swagger"; // Set the Swagger UI at the /swagger endpoint
});

//app.UseAuthentication(); // If using authentication
//app.UseAuthorization(); // Apply authorization

app.MapControllers();

app.Run();