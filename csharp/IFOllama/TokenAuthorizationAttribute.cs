using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace IFOllama
{
    public class TokenizedAuthorizationAttribute : AuthorizationHandler<IAuthorizationRequirement>, IAuthorizationHandler
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IAuthorizationRequirement requirement)
        {
            var token = context.User.FindFirstValue("Token");

            if (string.IsNullOrEmpty(token))
                return;

            // Validate the token (for example, by calling a validation endpoint or using an IdentityServer library)
            // For simplicity, let's just check that the token is not empty
            if (!string.IsNullOrEmpty(token))
            {
                var claimsIdentity = new ClaimsIdentity([new Claim("Token", token)]);
                var _ = new ClaimsPrincipal([claimsIdentity]);

                context.Succeed(requirement);
                return;
            }

            var groupClaim = context.User.FindFirstValue("http://schemas.microsoft.com/ws/2008/06/identity/claims/groups");

            if (groupClaim == "IntelligenceUsers")
            {
                context.Succeed(requirement);
            }

            await Task.CompletedTask;
        }
    }
}