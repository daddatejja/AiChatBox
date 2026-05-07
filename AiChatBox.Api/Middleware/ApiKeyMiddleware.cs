using AiChatBox.Api.Models;
using AiChatBox.Api.Services;

namespace AiChatBox.Api.Middleware
{
    public class ApiKeyMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, ApiKeyService apiKeyService)
        {
            if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
            {
                var rawKey = apiKeyValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(rawKey))
                {
                    var project = await apiKeyService.ValidateApiKeyAsync(rawKey);
                    if (project != null)
                    {
                        context.Items["CurrentProject"] = project;
                    }
                    else
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { error = "Invalid API Key" });
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
