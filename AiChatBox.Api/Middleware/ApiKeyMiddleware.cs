using System.Security.Claims;
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
                context.Request.Path.StartsWithSegments("/api/firecrawl/webhook") ||
                context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/dashboard"))
            {
                await _next(context);
                return;
            }

            // 1. Check for X-Master-Key (Partner Administrative Operations)
            if (context.Request.Headers.TryGetValue("X-Master-Key", out var masterKeyValues))
            {
                var rawMasterKey = masterKeyValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(rawMasterKey))
                {
                    var partner = await apiKeyService.ValidateMasterKeyAsync(rawMasterKey);
                    if (partner != null)
                    {
                        context.Items["CurrentPartner"] = partner;
                        
                        var claims = new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, partner.OwnerId),
                            new Claim(ClaimTypes.Email, partner.Owner?.Email ?? ""),
                            new Claim(ClaimTypes.Role, "PartnerDeveloper"),
                            new Claim("partner_id", partner.Id.ToString())
                        };
                        var identity = new ClaimsIdentity(claims, "X-Master-Key");
                        context.User = new ClaimsPrincipal(identity);
                    }
                    else
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { error = "Invalid Master API Key" });
                        return;
                    }
                }
            }

            // 2. Check for X-Api-Key (Tenant/Project Widget Operations)
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
