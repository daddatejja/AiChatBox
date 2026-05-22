using System.Security.Claims;
using AiChatBox.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Filters
{
    public class ProjectScopeFilter : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var scopeClaim = user.FindFirst("project_scope");
            if (scopeClaim != null)
            {
                var scopedProjectIdStr = scopeClaim.Value;
                if (Guid.TryParse(scopedProjectIdStr, out var scopedProjectId))
                {
                    // 1. Check direct projectId parameters (e.g. projectId in route or query)
                    if (context.ActionArguments.TryGetValue("projectId", out var projectIdVal) && projectIdVal is Guid projectId)
                    {
                        if (projectId != scopedProjectId)
                        {
                            context.Result = new ForbidResult();
                            return;
                        }
                    }

                    // 2. Check general "id" parameters based on the controller name
                    if (context.ActionArguments.TryGetValue("id", out var idVal) && idVal is Guid id)
                    {
                        var controllerName = context.RouteData.Values["controller"]?.ToString();
                        var db = context.HttpContext.RequestServices.GetRequiredService<ChatDbContext>();

                        if (controllerName == "Project")
                        {
                            if (id != scopedProjectId)
                            {
                                context.Result = new ForbidResult();
                                return;
                            }
                        }
                        else if (controllerName == "Configuration")
                        {
                            var belongs = await db.Configurations.AnyAsync(c => c.Id == id && c.ProjectId == scopedProjectId);
                            if (!belongs)
                            {
                                context.Result = new ForbidResult();
                                return;
                            }
                        }
                        else if (controllerName == "Rule")
                        {
                            var belongs = await db.ConversationRules.AnyAsync(r => r.Id == id && r.ProjectId == scopedProjectId);
                            if (!belongs)
                            {
                                context.Result = new ForbidResult();
                                return;
                            }
                        }
                        else if (controllerName == "KnowledgeBase")
                        {
                            var belongs = await db.KnowledgeDocuments.AnyAsync(d => d.Id == id && d.ProjectId == scopedProjectId);
                            if (!belongs)
                            {
                                context.Result = new ForbidResult();
                                return;
                            }
                        }
                        else if (controllerName == "File")
                        {
                            var file = await db.UploadedFiles.FindAsync(id);
                            if (file != null)
                            {
                                var currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                var isOwner = !string.IsNullOrEmpty(currentUserId) && file.UserId == currentUserId;
                                var isProjectFile = file.UserId == $"project-{scopedProjectId}";
                                if (!isOwner && !isProjectFile)
                                {
                                    context.Result = new ForbidResult();
                                    return;
                                }
                            }
                        }
                    }
                }
            }

            await next();
        }
    }
}
