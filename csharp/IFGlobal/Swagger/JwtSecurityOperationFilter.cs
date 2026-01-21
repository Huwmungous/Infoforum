using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace IFGlobal.Swagger;

/// <summary>
/// Operation filter that adds JWT Bearer security to [Authorize] endpoints.
/// </summary>
public class JwtSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check for [Authorize] on method or controller
        var hasAuthorize = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(true)
            .OfType<AuthorizeAttribute>()
            .Any() == true
            || context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>()
                .Any();

        // Check for [AllowAnonymous] on method
        var hasAllowAnonymous = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<AllowAnonymousAttribute>()
            .Any();

        if (hasAuthorize && !hasAllowAnonymous)
        {
            // Add 401/403 responses
            operation.Responses ??= [];
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorised" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

            // Add security requirement using OpenAPI 2.x syntax
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
            });
        }
    }
}