using AiChatBox.Api.Models;
using AiChatBox.Api.Services;

namespace AiChatBox.Api.Middleware
{
    public class ApiKeyMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context, ApiKeyService apiKeyService)
        {
            if (context.Request.Path.StartsWithSegments("/api/auth") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/dashboard"))
            {
                await _next(context);
                return;
            }

            if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyValues))
            {
                var rawKey = apiKeyValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(rawKey))
                {
                    var origin = context.Request.Headers["Origin"].ToString();
                    var (project, configuration, apiKey) = await apiKeyService.ValidateApiKeyAsync(rawKey, origin);
                    if (project != null)
                    {
                        context.Items["CurrentProject"] = project;
                        context.Items["CurrentApiKey"] = apiKey;
                        if (configuration != null)
                            context.Items["CurrentConfiguration"] = configuration;
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
