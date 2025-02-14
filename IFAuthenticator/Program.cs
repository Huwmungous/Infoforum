using IFGlobal;
using Microsoft.OpenApi.Models;

int port = PortResolver.GetPort("IFAuthenticator");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Configure Kestrel to listen on HTTP
    serverOptions.ListenAnyIP(port);
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "IFAuthenticator API", Version = "v1" });
});

// Add CORS services with a specific policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        policyBuilder => policyBuilder
            .WithOrigins(
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

// Enable Swagger middleware
app.UseSwagger(); // Swagger must be accessible without authorization
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "IFAuthenticator API V1");
    c.RoutePrefix = "swagger"; // Set Swagger UI at the /swagger endpoint
});

app.UseAuthentication(); // If using authentication
app.UseAuthorization(); // Apply authorization

app.MapControllers();

app.Run($"http://*:{port}"); // Listen on HTTP port