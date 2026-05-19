using System.Security.Claims;
using AiChatBox.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController(ChatDbContext db) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        /// <summary>
        /// Returns aggregated analytics for the user's projects.
        /// Query params: projectId (optional), days (default 30).
        /// </summary>
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview(Guid? projectId = null, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);

            // Scoped to user's projects
            var projectsQuery = _db.Projects.Where(p => p.UserId == UserId);
            if (projectId.HasValue)
                projectsQuery = projectsQuery.Where(p => p.Id == projectId.Value);

            var projectIds = await projectsQuery.Select(p => p.Id).ToListAsync();

            if (projectIds.Count == 0)
                return Ok(new { });

            // Logs in range
            var logsQuery = _db.AiRequestLogs
                .Where(l => l.ProjectId.HasValue && projectIds.Contains(l.ProjectId.Value) && l.CreatedAt >= since);

            // Sessions in range
            var sessionsQuery = _db.ChatSessions
                .Where(s => s.ProjectId.HasValue && projectIds.Contains(s.ProjectId.Value) && s.CreatedAt >= since);

            // Messages in range
            var messagesQuery = _db.ChatMessages
                .Where(m => m.Session != null && m.Session.ProjectId.HasValue 
                    && projectIds.Contains(m.Session.ProjectId.Value) && m.CreatedAt >= since);

            // Aggregate
            var totalRequests = await logsQuery.CountAsync();
            var totalSessions = await sessionsQuery.CountAsync();
            var totalMessages = await messagesQuery.CountAsync();
            var totalInputTokens = await logsQuery.SumAsync(l => (long)l.InputTokens);
            var totalOutputTokens = await logsQuery.SumAsync(l => (long)l.OutputTokens);
            var avgResponseMs = totalRequests > 0 ? await logsQuery.AverageAsync(l => l.DurationMs) : 0;
            var errorCount = await logsQuery.CountAsync(l => l.ErrorMessage != null);

            // Feedback
            var thumbsUp = await messagesQuery.CountAsync(m => m.Feedback == 1);
            var thumbsDown = await messagesQuery.CountAsync(m => m.Feedback == -1);

            // Rule matches
            var ruleMatches = await logsQuery.CountAsync(l => l.Provider == "rules");

            return Ok(new
            {
                totalRequests,
                totalSessions,
                totalMessages,
                totalInputTokens,
                totalOutputTokens,
                avgResponseMs = Math.Round(avgResponseMs, 1),
                errorCount,
                errorRate = totalRequests > 0 ? Math.Round((double)errorCount / totalRequests * 100, 2) : 0,
                thumbsUp,
                thumbsDown,
                feedbackScore = (thumbsUp + thumbsDown) > 0 
                    ? Math.Round((double)thumbsUp / (thumbsUp + thumbsDown) * 100, 1) : 0,
                ruleMatches,
                days
            });
        }

        /// <summary>Daily volume breakdown (messages per day, sessions per day).</summary>
        [HttpGet("volume")]
        public async Task<IActionResult> GetVolume(Guid? projectId = null, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            var projectIds = await GetProjectIds(projectId);
            if (projectIds.Count == 0) return Ok(Array.Empty<object>());

            var dailyLogs = await _db.AiRequestLogs
                .Where(l => l.ProjectId.HasValue && projectIds.Contains(l.ProjectId.Value) && l.CreatedAt >= since)
                .GroupBy(l => l.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key,
                    requests = g.Count(),
                    inputTokens = g.Sum(l => (long)l.InputTokens),
                    outputTokens = g.Sum(l => (long)l.OutputTokens),
                    avgDurationMs = Math.Round(g.Average(l => l.DurationMs), 1),
                    errors = g.Count(l => l.ErrorMessage != null)
                })
                .OrderBy(d => d.date)
                .ToListAsync();

            var dailySessions = await _db.ChatSessions
                .Where(s => s.ProjectId.HasValue && projectIds.Contains(s.ProjectId.Value) && s.CreatedAt >= since)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new { date = g.Key, sessions = g.Count() })
                .OrderBy(d => d.date)
                .ToListAsync();

            // Merge into one timeline
            var allDates = Enumerable.Range(0, days).Select(d => DateTime.UtcNow.Date.AddDays(-days + 1 + d)).ToList();
            var logsByDate = dailyLogs.ToDictionary(d => d.date);
            var sessionsByDate = dailySessions.ToDictionary(d => d.date);

            var result = allDates.Select(date => new
            {
                date = date.ToString("yyyy-MM-dd"),
                requests = logsByDate.TryGetValue(date, out var l) ? l.requests : 0,
                inputTokens = logsByDate.TryGetValue(date, out var li) ? li.inputTokens : 0L,
                outputTokens = logsByDate.TryGetValue(date, out var lo) ? lo.outputTokens : 0L,
                avgDurationMs = logsByDate.TryGetValue(date, out var ld) ? ld.avgDurationMs : 0,
                errors = logsByDate.TryGetValue(date, out var le) ? le.errors : 0,
                sessions = sessionsByDate.TryGetValue(date, out var s) ? s.sessions : 0
            });

            return Ok(result);
        }

        /// <summary>Provider usage breakdown.</summary>
        [HttpGet("providers")]
        public async Task<IActionResult> GetProviderBreakdown(Guid? projectId = null, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            var projectIds = await GetProjectIds(projectId);
            if (projectIds.Count == 0) return Ok(Array.Empty<object>());

            var breakdown = await _db.AiRequestLogs
                .Where(l => l.ProjectId.HasValue && projectIds.Contains(l.ProjectId.Value) && l.CreatedAt >= since && l.Provider != null)
                .GroupBy(l => l.Provider!)
                .Select(g => new
                {
                    provider = g.Key,
                    requests = g.Count(),
                    inputTokens = g.Sum(l => (long)l.InputTokens),
                    outputTokens = g.Sum(l => (long)l.OutputTokens),
                    avgDurationMs = Math.Round(g.Average(l => l.DurationMs), 1),
                    errors = g.Count(l => l.ErrorMessage != null)
                })
                .OrderByDescending(p => p.requests)
                .ToListAsync();

            return Ok(breakdown);
        }

        /// <summary>Model usage breakdown.</summary>
        [HttpGet("models")]
        public async Task<IActionResult> GetModelBreakdown(Guid? projectId = null, int days = 30)
        {
            var since = DateTime.UtcNow.AddDays(-days);
            var projectIds = await GetProjectIds(projectId);
            if (projectIds.Count == 0) return Ok(Array.Empty<object>());

            var breakdown = await _db.AiRequestLogs
                .Where(l => l.ProjectId.HasValue && projectIds.Contains(l.ProjectId.Value) && l.CreatedAt >= since && l.Model != null)
                .GroupBy(l => new { l.Provider, l.Model })
                .Select(g => new
                {
                    provider = g.Key.Provider ?? "unknown",
                    model = g.Key.Model ?? "unknown",
                    requests = g.Count(),
                    inputTokens = g.Sum(l => (long)l.InputTokens),
                    outputTokens = g.Sum(l => (long)l.OutputTokens),
                    avgDurationMs = Math.Round(g.Average(l => l.DurationMs), 1)
                })
                .OrderByDescending(m => m.requests)
                .ToListAsync();

            return Ok(breakdown);
        }

        /// <summary>Recent negative feedback messages for quality review.</summary>
        [HttpGet("feedback")]
        public async Task<IActionResult> GetFeedback(Guid? projectId = null, int limit = 50, int? feedbackFilter = null)
        {
            var projectIds = await GetProjectIds(projectId);
            if (projectIds.Count == 0) return Ok(Array.Empty<object>());

            var query = _db.ChatMessages
                .Include(m => m.Session)
                .Where(m => m.Session != null && m.Session.ProjectId.HasValue 
                    && projectIds.Contains(m.Session.ProjectId.Value) 
                    && m.Feedback.HasValue);

            if (feedbackFilter.HasValue)
                query = query.Where(m => m.Feedback == feedbackFilter.Value);

            var messages = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.SessionId,
                    m.Role,
                    content = m.Content.Length > 500 ? m.Content.Substring(0, 500) + "..." : m.Content,
                    m.Feedback,
                    m.CreatedAt,
                    projectId = m.Session!.ProjectId
                })
                .ToListAsync();

            return Ok(messages);
        }

        private async Task<List<Guid>> GetProjectIds(Guid? projectId)
        {
            var query = _db.Projects.Where(p => p.UserId == UserId);
            if (projectId.HasValue)
                query = query.Where(p => p.Id == projectId.Value);
            return await query.Select(p => p.Id).ToListAsync();
        }
    }
}
