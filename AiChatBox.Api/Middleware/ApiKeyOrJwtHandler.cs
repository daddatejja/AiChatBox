using Microsoft.AspNetCore.Authorization;

namespace AiChatBox.Api.Middleware
{
    public class ApiKeyOrJwtRequirement : IAuthorizationRequirement { }

    public class ApiKeyOrJwtHandler : AuthorizationHandler<ApiKeyOrJwtRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ApiKeyOrJwtRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated == true ||
                context.Resource is HttpContext httpContext && httpContext.Items["CurrentProject"] != null)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
            return Task.CompletedTask;
        }
    }
}
