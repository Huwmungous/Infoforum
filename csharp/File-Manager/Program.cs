using FileManager.Api.Services;
using IFGlobal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

int port = PortResolver.GetPort("File-Manager");

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(port);
});

var configuration = builder.Configuration;

// Add CORS services with a specific policy.
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

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Authentication:Authority"];
        options.Audience = configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = bool.Parse(configuration["Authentication:RequireHttps"]);
        options.SaveToken = true;
    });

// Register application services
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<ILdapAuthorizationService, LdapAuthorizationService>();

builder.Services.AddControllers();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

//app.UseHttpsRedirection();
//app.UseStaticFiles();

// Enable CORS before authentication and authorization.
app.UseCors("AllowSpecificOrigins");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
