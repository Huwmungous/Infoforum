using FileManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Configure CORS policy for Angular application
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policyBuilder =>
    {
        policyBuilder.WithOrigins(configuration["AllowedOrigins"].Split(','))
                     .AllowAnyMethod()
                     .AllowAnyHeader()
                     .AllowCredentials();
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Authentication:Authority"];
        options.Audience = configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = bool.Parse(configuration["Authentication:RequireHttps"]);
        options.SaveToken = true;

        // If using a self-signed cert for development
        if (bool.Parse(configuration["Authentication:ValidateIssuerSigningKey"]))
        {
            var signingKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(configuration["Authentication:SigningKey"]));

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey
            };
        }
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("CorsPolicy");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
