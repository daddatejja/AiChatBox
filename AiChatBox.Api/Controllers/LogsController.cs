using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
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
                    l.IsPinned,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items = logs, total, offset, limit });
        }

        [HttpGet("trace/{sessionId}")]
        public async Task<IActionResult> GetSessionTrace(Guid sessionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not found." });
            }

            var logs = await _db.AiRequestLogs
                .Where(l => l.SessionId == sessionId && l.Project != null && l.Project.UserId == userId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new
                {
                    l.Id,
                    l.SessionId,
                    l.Endpoint,
                    l.InputTokens,
                    l.OutputTokens,
                    l.DurationMs,
                    l.RawRequest,
                    l.RawResponse,
                    l.ErrorMessage,
                    l.IsPinned,
                    l.CreatedAt
                })
                .ToListAsync();

            return Ok(logs);
        }

        [HttpPost("{id}/pin")]
        public async Task<IActionResult> TogglePin(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User not found." });
            }

            var log = await _db.AiRequestLogs
                .Include(l => l.Project)
                .FirstOrDefaultAsync(l => l.Id == id && l.Project != null && l.Project.UserId == userId);

            if (log == null)
            {
                return NotFound(new { error = "Log not found." });
            }

            log.IsPinned = !log.IsPinned;

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = log.IsPinned ? "pin_log" : "unpin_log",
                TargetId = log.Id.ToString(),
                Details = $"Endpoint: {log.Endpoint}, Session: {log.SessionId}",
                CreatedAt = DateTime.UtcNow
            };
            _db.AuditLogs.Add(auditLog);

            await _db.SaveChangesAsync();

            return Ok(new { isPinned = log.IsPinned });
        }
    }
}
