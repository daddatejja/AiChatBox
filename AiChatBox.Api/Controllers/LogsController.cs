using System.Security.Claims;
using AiChatBox.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LogsController(ChatDbContext db) : ControllerBase
    {
        private readonly ChatDbContext _db = db;

        [HttpGet]
        public async Task<IActionResult> GetLogs(
            int offset = 0, 
            int limit = 50, 
            Guid? projectId = null, 
            string? search = null, 
            string? sortField = "createdAt", 
            int sortOrder = -1)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not found." });
            }

            var query = _db.AiRequestLogs.Where(l => l.Project != null && l.Project.UserId == userId);

            if (projectId.HasValue)
            {
                query = query.Where(l => l.ProjectId == projectId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(l => 
                    (l.Endpoint != null && l.Endpoint.ToLower().Contains(lowerSearch)) ||
                    (l.RawRequest != null && l.RawRequest.ToLower().Contains(lowerSearch)) ||
                    (l.RawResponse != null && l.RawResponse.ToLower().Contains(lowerSearch)) ||
                    (l.ErrorMessage != null && l.ErrorMessage.ToLower().Contains(lowerSearch))
                );
            }

            // Apply Sorting
            query = sortField switch
            {
                "durationMs" => sortOrder == 1 ? query.OrderBy(l => l.DurationMs) : query.OrderByDescending(l => l.DurationMs),
                "inputTokens" => sortOrder == 1 ? query.OrderBy(l => l.InputTokens) : query.OrderByDescending(l => l.InputTokens),
                "outputTokens" => sortOrder == 1 ? query.OrderBy(l => l.OutputTokens) : query.OrderByDescending(l => l.OutputTokens),
                "endpoint" => sortOrder == 1 ? query.OrderBy(l => l.Endpoint) : query.OrderByDescending(l => l.Endpoint),
                _ => sortOrder == 1 ? query.OrderBy(l => l.CreatedAt) : query.OrderByDescending(l => l.CreatedAt)
            };

            var total = await query.CountAsync();

            var logs = await query
                .Skip(offset)
                .Take(limit)
                .Select(l => new
                {
                    l.Id,
                    l.SessionId,
                    l.ProjectId,
                    l.UserId,
                    l.Endpoint,
                    l.InputTokens,
                    l.OutputTokens,
                    l.DurationMs,
                    l.RawRequest,
                    l.RawResponse,
                    l.ErrorMessage,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items = logs, total, offset, limit });
        }
    }
}
