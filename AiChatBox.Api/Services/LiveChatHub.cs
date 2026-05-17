using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// SignalR hub for real-time agent ↔ user chat during human handoff.
    /// Agents connect from the Dashboard (JWT auth).
    /// Widget users connect using their session group (API key auth).
    /// </summary>
    public class LiveChatHub(
        HandoffService handoffService,
        ChatDbContext db,
        ApiKeyService apiKeyService,
        ILogger<LiveChatHub> logger) : Hub
    {
        private readonly HandoffService _handoffService = handoffService;
        private readonly ChatDbContext _db = db;
        private readonly ApiKeyService _apiKeyService = apiKeyService;
        private readonly ILogger<LiveChatHub> _logger = logger;

        // ─── Agent Methods (Dashboard, JWT auth) ───

        /// <summary>
        /// Agent joins the notification pool for a specific project.
        /// They will receive NewSessionQueued events.
        /// </summary>
        [Authorize]
        public async Task JoinAgentPool(string projectId)
        {
            if (!Guid.TryParse(projectId, out var pid)) return;

            var agentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (agentId == null) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"agents-{pid}");
            _logger.LogInformation("Agent {AgentId} joined pool for project {ProjectId}", agentId, projectId);
        }

        /// <summary>
        /// Agent claims a queued session.
        /// </summary>
        [Authorize]
        public async Task ClaimSession(string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var sid)) return;

            var agentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (agentId == null) return;

            var success = await _handoffService.ClaimSessionAsync(sid, agentId);
            if (success)
            {
                // Agent joins the session group to receive user messages
                await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sid}");
                await Clients.Caller.SendAsync("SessionClaimResult", new { sessionId = sid, success = true });
            }
            else
            {
                await Clients.Caller.SendAsync("SessionClaimResult", new { sessionId = sid, success = false, error = "Session is no longer available." });
            }
        }

        /// <summary>
        /// Agent sends a text message to the user.
        /// </summary>
        [Authorize]
        public async Task SendAgentMessage(string sessionId, string message)
        {
            if (!Guid.TryParse(sessionId, out var sid) || string.IsNullOrWhiteSpace(message)) return;

            var agentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (agentId == null) return;

            try
            {
                await _handoffService.SendAgentMessageAsync(sid, agentId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending agent message for session {SessionId}", sessionId);
                await Clients.Caller.SendAsync("ReceiveError", "Failed to send message.");
            }
        }

        /// <summary>
        /// Agent resolves the session (ends handoff).
        /// </summary>
        [Authorize]
        public async Task ResolveSession(string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var sid)) return;

            var agentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (agentId == null) return;

            await _handoffService.ResolveSessionAsync(sid, agentId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sid}");
        }

        /// <summary>
        /// Agent returns session back to AI mode.
        /// </summary>
        [Authorize]
        public async Task ReturnToAi(string sessionId)
        {
            if (!Guid.TryParse(sessionId, out var sid)) return;

            var agentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (agentId == null) return;

            await _handoffService.ReturnToAiAsync(sid, agentId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sid}");
        }

        // ─── Widget User Methods ───

        /// <summary>
        /// Widget joins a session group to receive real-time agent messages.
        /// Authenticated via API key.
        /// </summary>
        public async Task JoinSession(string sessionId, string apiKey)
        {
            if (!Guid.TryParse(sessionId, out var sid) || string.IsNullOrEmpty(apiKey)) return;

            // Validate API key
            var origin = Context.GetHttpContext()?.Request.Headers.Origin.ToString();
            var (project, _, _) = await _apiKeyService.ValidateApiKeyAsync(apiKey, origin);
            if (project == null)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Unauthorized.");
                return;
            }

            // Verify session belongs to this project
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sid && s.ProjectId == project.Id);
            if (session == null)
            {
                await Clients.Caller.SendAsync("ReceiveError", "Session not found.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sid}");
            _logger.LogInformation("Widget user joined session group {SessionId}", sessionId);

            // Send current handoff status
            await Clients.Caller.SendAsync("HandoffStatus", new
            {
                sessionId = session.Id,
                status = session.HandoffStatus,
                hasAgent = session.AgentId != null
            });
        }

        /// <summary>
        /// Widget user sends a message during handoff.
        /// Message is saved and forwarded to the agent.
        /// </summary>
        public async Task SendUserMessage(string sessionId, string message, string apiKey)
        {
            if (!Guid.TryParse(sessionId, out var sid) || string.IsNullOrWhiteSpace(message)) return;

            var origin = Context.GetHttpContext()?.Request.Headers.Origin.ToString();
            var (project, _, _) = await _apiKeyService.ValidateApiKeyAsync(apiKey, origin);
            if (project == null) return;

            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sid && s.ProjectId == project.Id && s.HandoffStatus == "active");
            if (session == null) return;

            var msg = new ChatMessage
            {
                SessionId = sid,
                Role = "user",
                Content = message,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(msg);
            session.LastMessageAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify agent via session group
            await Clients.Group($"session-{sid}").SendAsync("ReceiveUserMessage", new
            {
                id = msg.Id,
                sessionId = sid,
                content = msg.Content,
                createdAt = msg.CreatedAt,
                role = "user"
            });
        }
        /// <summary>
        /// Broadcasts that the agent is typing.
        /// </summary>
        [Authorize]
        public async Task SendAgentTyping(string sessionId, bool isTyping)
        {
            if (!Guid.TryParse(sessionId, out var sid)) return;
            await Clients.Group($"session-{sid}").SendAsync("ReceiveAgentTyping", new { sessionId = sid, isTyping });
        }

        /// <summary>
        /// Broadcasts that the user is typing.
        /// </summary>
        public async Task SendUserTyping(string sessionId, bool isTyping, string apiKey)
        {
            if (!Guid.TryParse(sessionId, out var sid) || string.IsNullOrEmpty(apiKey)) return;
            await Clients.Group($"session-{sid}").SendAsync("ReceiveUserTyping", new { sessionId = sid, isTyping });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client {ConnectionId} disconnected from LiveChatHub", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
