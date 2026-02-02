using IFGlobal.Configuration;
using IFGlobal.WebServices;
using Microsoft.AspNetCore.Authorization;

namespace ChitterChatterDistribution;

public class Program
{
    public static async Task Main(string[] args)
    {
        var options = new ServiceFactoryOptions
        {
            ServiceName = "ChitterChatterDistribution",
            Description = "ChitterChatter Download Server",
            UseAuthentication = true,
            AuthType = AuthType.Service,
            // PathBase = "/infoforum/download",
            ConfigureServices = (services, context) =>
            {
                services.AddRazorPages();
                services.Configure<DistributionOptions>(opts =>
                {
                    opts.DistributionPath = context.Configuration["DistributionPath"]
                        ?? Path.Combine(AppContext.BaseDirectory, "dist");
                });

                // Register custom authorization handler for nginx proxy auth
                services.AddSingleton<IAuthorizationHandler, NginxProxyAuthorizationHandler>();

                // Configure authorization policies for defense-in-depth
                services.AddAuthorizationBuilder()
                    .AddPolicy("NginxOrJwt", policy =>
                    {
                        policy.Requirements.Add(new NginxOrJwtRequirement());
                    });
            },
            ConfigurePipeline = (app, context) =>
            {
                app.UseStaticFiles();
                app.MapRazorPages();
            }
        };

        var app = await ServiceFactory.CreateAsync(options);
        await app.RunAsync();
    }
}

public class DistributionOptions
{
    public string DistributionPath { get; set; } = "dist";
}

/// <summary>
/// Authorization requirement that accepts either JWT or nginx proxy authentication.
/// Defense-in-depth: nginx validates first, then ASP.NET validates JWT if present,
/// or accepts nginx's validation marker for session-based auth.
/// </summary>
public class NginxOrJwtRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Handler that succeeds if:
/// 1. User is authenticated via JWT (standard Bearer token), OR
/// 2. Request has X-Nginx-Proxy header set to "authenticated" (nginx validated the session)
/// </summary>
public class NginxProxyAuthorizationHandler : AuthorizationHandler<NginxOrJwtRequirement>
{
    private readonly ILogger<NginxProxyAuthorizationHandler> _logger;

    public NginxProxyAuthorizationHandler(ILogger<NginxProxyAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        NginxOrJwtRequirement requirement)
    {
        // Check if authenticated via JWT
        if(context.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogDebug("Authorization succeeded via JWT for user {User}", context.User.Identity.Name);
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if nginx validated the request
        if(context.Resource is HttpContext httpContext)
        {
            if(httpContext.Request.Headers.TryGetValue("X-Nginx-Proxy", out var nginxHeader)
                && nginxHeader == "authenticated")
            {
                _logger.LogDebug("Authorization succeeded via nginx proxy validation");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
        }

        _logger.LogWarning("Authorization failed - no JWT and no nginx proxy header");
        return Task.CompletedTask;
    }
}