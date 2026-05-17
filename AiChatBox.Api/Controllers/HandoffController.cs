using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HandoffController(HandoffService handoffService, ChatDbContext db) : ControllerBase
    {
        private readonly HandoffService _handoffService = handoffService;
        private readonly ChatDbContext _db = db;

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        /// <summary>
        /// Get all queued sessions waiting for an agent.
        /// </summary>
        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue([FromQuery] Guid? projectId)
        {
            var sessions = await _handoffService.GetQueuedSessionsAsync(projectId);
            return Ok(sessions);
        }

        /// <summary>
        /// Get sessions currently claimed by the authenticated agent.
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var sessions = await _handoffService.GetActiveSessionsAsync(UserId);
            return Ok(sessions);
        }

        /// <summary>
        /// Get all handoff sessions (queued + active) for the agent dashboard.
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAll([FromQuery] Guid? projectId)
        {
            var queued = await _handoffService.GetQueuedSessionsAsync(projectId);
            var active = await _handoffService.GetActiveSessionsAsync(UserId);
            return Ok(new { queued, active });
        }

        /// <summary>
        /// Get messages for a handoff session.
        /// </summary>
        [HttpGet("session/{sessionId}/messages")]
        public async Task<IActionResult> GetMessages(Guid sessionId)
        {
            var session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return NotFound();

            var messages = await _db.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.CreatedAt,
                    m.Feedback
                })
                .ToListAsync();

            return Ok(new
            {
                session = new
                {
                    session.Id,
                    session.UserId,
                    session.ProjectId,
                    session.HandoffStatus,
                    session.AgentId,
                    session.QueuedAt,
                    session.ClaimedAt
                },
                messages
            });
        }

        /// <summary>
        /// Get the count of queued sessions (for sidebar badge).
        /// </summary>
        [HttpGet("queue-count")]
        public async Task<IActionResult> GetQueueCount([FromQuery] Guid? projectId)
        {
            var query = _db.ChatSessions.Where(s => s.HandoffStatus == "queued");
            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId);

            var count = await query.CountAsync();
            return Ok(new { count });
        }
    }
}
