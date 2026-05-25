using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiChatBox.Api.Data;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Services
{
    public class HandoffService(
        ChatDbContext db,
        IHubContext<LiveChatHub> hubContext,
        IntentClassifierService classifier,
        IEnumerable<IChannelAdapter> adapters,
        ILogger<HandoffService> logger)
    {
        private readonly ChatDbContext _db = db;
        private readonly IHubContext<LiveChatHub> _hubContext = hubContext;
        private readonly IntentClassifierService _classifier = classifier;
        private readonly IEnumerable<IChannelAdapter> _adapters = adapters;
        private readonly ILogger<HandoffService> _logger = logger;

        /// <summary>
        /// Checks if a user message should trigger escalation to a human agent.
        /// Uses a two-phase approach:
        /// 1. Fast keyword check (instant, zero-cost, backward-compatible)
        /// 2. LLM-based intent classification using escalation criteria (if keywords didn't match)
        /// </summary>
        public async Task<HandoffCheckResult> ShouldTriggerHandoffAsync(
            string message,
            ProjectConfiguration? config,
            List<string>? recentMessages = null,
            CancellationToken cancellationToken = default)
        {
            if (config == null || !config.HandoffEnabled)
                return HandoffCheckResult.NoMatch;

            // ─── Phase 1: Fast keyword check (backward-compatible) ───
            if (!string.IsNullOrEmpty(config.HandoffTriggerKeywords))
            {
                var keywords = config.HandoffTriggerKeywords
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var lowerMessage = message.ToLowerInvariant();
                if (keywords.Any(kw => lowerMessage.Contains(kw.ToLowerInvariant())))
                {
                    _logger.LogInformation("Handoff triggered by keyword match");
                    return new HandoffCheckResult(true, 1.0, "keyword", "Keyword match");
                }
            }

            // ─── Phase 2: LLM-based escalation detection ───
            if (!string.IsNullOrEmpty(config.HandoffEscalationCriteria))
            {
                try
                {
                    var result = await _classifier.CheckEscalationAsync(
                        message,
                        recentMessages,
                        config.HandoffEscalationCriteria,
                        config,
                        cancellationToken);

                    if (result.IntentId == "escalation" && result.Confidence >= config.HandoffConfidenceThreshold)
                    {
                        _logger.LogInformation(
                            "Handoff triggered by intent classification (confidence={Confidence:F2}, threshold={Threshold:F2})",
                            result.Confidence, config.HandoffConfidenceThreshold);
                        return new HandoffCheckResult(true, result.Confidence, "intent", result.Reasoning);
                    }

                    _logger.LogDebug(
                        "Escalation check returned intent='{Intent}' confidence={Confidence:F2} (threshold={Threshold:F2}) — no escalation",
                        result.IntentId, result.Confidence, config.HandoffConfidenceThreshold);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM escalation check failed, not escalating");
                }
            }

            return HandoffCheckResult.NoMatch;
        }

        /// <summary>
        /// Places a session in the handoff queue and notifies agents.
        /// </summary>
        public async Task<bool> QueueSessionAsync(Guid sessionId)
        {
            var session = await _db.ChatSessions
                .Include(s => s.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return false;
            if (session.HandoffStatus is "queued" or "active") return false;

            session.HandoffStatus = "queued";
            session.QueuedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify agents watching this project
            var groupName = $"agents-{session.ProjectId}";
            var lastMessage = session.Messages.FirstOrDefault()?.Content ?? "";

            await _hubContext.Clients.Group(groupName).SendAsync("NewSessionQueued", new
            {
                sessionId = session.Id,
                userId = session.UserId,
                projectId = session.ProjectId,
                configurationId = session.ConfigurationId,
                lastMessage = lastMessage.Length > 200 ? lastMessage[..200] + "..." : lastMessage,
                queuedAt = session.QueuedAt
            });

            _logger.LogInformation("Session {SessionId} queued for handoff", sessionId);
            return true;
        }

        /// <summary>
        /// Agent claims a queued session.
        /// </summary>
        public async Task<bool> ClaimSessionAsync(Guid sessionId, string agentId)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null || session.HandoffStatus != "queued") return false;

            session.HandoffStatus = "active";
            session.AgentId = agentId;
            session.ClaimedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify agent pool
            var groupName = $"agents-{session.ProjectId}";
            await _hubContext.Clients.Group(groupName).SendAsync("SessionClaimed", new
            {
                sessionId = session.Id,
                agentId
            });

            // Notify the user's session group
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("AgentJoined", new
            {
                sessionId = session.Id,
                message = "A support agent has joined the conversation."
            });

            _logger.LogInformation("Session {SessionId} claimed by agent {AgentId}", sessionId, agentId);
            return true;
        }

        /// <summary>
        /// Resolves a handoff session and returns it to AI mode.
        /// </summary>
        public async Task<bool> ResolveSessionAsync(Guid sessionId, string agentId)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.AgentId == agentId);
            if (session == null || session.HandoffStatus != "active") return false;

            session.HandoffStatus = "resolved";
            await _db.SaveChangesAsync();

            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("SessionResolved", new
            {
                sessionId = session.Id,
                message = "The support session has ended. You're now chatting with AI again."
            });

            _logger.LogInformation("Session {SessionId} resolved by agent {AgentId}", sessionId, agentId);
            return true;
        }

        /// <summary>
        /// Returns a session back to AI mode without resolving (agent gives up).
        /// </summary>
        public async Task<bool> ReturnToAiAsync(Guid sessionId, string agentId)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.AgentId == agentId);
            if (session == null) return false;

            session.HandoffStatus = "ai";
            session.AgentId = null;
            await _db.SaveChangesAsync();

            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReturnedToAi", new
            {
                sessionId = session.Id,
                message = "You've been returned to the AI assistant."
            });

            _logger.LogInformation("Session {SessionId} returned to AI by agent {AgentId}", sessionId, agentId);
            return true;
        }

        /// <summary>
        /// Gets all queued sessions for a project.
        /// </summary>
        public async Task<List<HandoffSessionDto>> GetQueuedSessionsAsync(Guid? projectId)
        {
            var query = _db.ChatSessions
                .Include(s => s.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(s => s.Project)
                .Include(s => s.Configuration)
                .Where(s => s.HandoffStatus == "queued");

            if (projectId.HasValue)
                query = query.Where(s => s.ProjectId == projectId);

            return await query.OrderBy(s => s.QueuedAt).Select(s => new HandoffSessionDto
            {
                SessionId = s.Id,
                UserId = s.UserId,
                Title = s.Title,
                ProjectId = s.ProjectId,
                ProjectName = s.Project != null ? s.Project.Name : "Unknown",
                ConfigurationName = s.Configuration != null ? s.Configuration.Name : null,
                HandoffStatus = s.HandoffStatus,
                AgentId = s.AgentId,
                QueuedAt = s.QueuedAt,
                ClaimedAt = s.ClaimedAt,
                LastMessage = s.Messages.Select(m => m.Content).FirstOrDefault() ?? "",
                MessageCount = s.Messages.Count
            }).ToListAsync();
        }

        /// <summary>
        /// Gets all active sessions for an agent.
        /// </summary>
        public async Task<List<HandoffSessionDto>> GetActiveSessionsAsync(string agentId)
        {
            return await _db.ChatSessions
                .Include(s => s.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .Include(s => s.Project)
                .Include(s => s.Configuration)
                .Where(s => s.AgentId == agentId && s.HandoffStatus == "active")
                .OrderByDescending(s => s.ClaimedAt)
                .Select(s => new HandoffSessionDto
                {
                    SessionId = s.Id,
                    UserId = s.UserId,
                    Title = s.Title,
                    ProjectId = s.ProjectId,
                    ProjectName = s.Project != null ? s.Project.Name : "Unknown",
                    ConfigurationName = s.Configuration != null ? s.Configuration.Name : null,
                    HandoffStatus = s.HandoffStatus,
                    AgentId = s.AgentId,
                    QueuedAt = s.QueuedAt,
                    ClaimedAt = s.ClaimedAt,
                    LastMessage = s.Messages.Select(m => m.Content).FirstOrDefault() ?? "",
                    MessageCount = s.Messages.Count
                }).ToListAsync();
        }

        /// <summary>
        /// Saves a message from an agent and notifies the user.
        /// </summary>
        public async Task<ChatMessage> SendAgentMessageAsync(Guid sessionId, string agentId, string message)
        {
            var session = await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.AgentId == agentId && s.HandoffStatus == "active");
            if (session == null) throw new InvalidOperationException("Session not found or not active.");

            var msg = new ChatMessage
            {
                SessionId = sessionId,
                Role = "agent",
                Content = message,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(msg);
            session.LastMessageAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify user via session group
            await _hubContext.Clients.Group($"session-{sessionId}").SendAsync("ReceiveAgentMessage", new
            {
                id = msg.Id,
                content = msg.Content,
                createdAt = msg.CreatedAt,
                role = "agent"
            });

            // If this is an external channel session, dispatch outbound message through the channel adapter
            if (!string.IsNullOrEmpty(session.ExternalSenderId) && session.UserId.StartsWith("external-"))
            {
                var parts = session.UserId.Split('-');
                if (parts.Length >= 2)
                {
                    var channel = parts[1];
                    var adapter = _adapters.FirstOrDefault(a => a.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
                    if (adapter != null)
                    {
                        try
                        {
                            await adapter.SendOutbound(new OutboundMessage
                            {
                                RecipientId = session.ExternalSenderId,
                                Text = message,
                                Channel = channel,
                                SessionId = session.Id,
                                ProjectId = session.ProjectId ?? Guid.Empty
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send outbound agent message over channel {Channel} for session {SessionId}", channel, sessionId);
                        }
                    }
                }
            }

            return msg;
        }
    }

    public class HandoffSessionDto
    {
        public Guid SessionId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public Guid? ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ConfigurationName { get; set; }
        public string HandoffStatus { get; set; } = "ai";
        public string? AgentId { get; set; }
        public DateTime? QueuedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public int MessageCount { get; set; }
    }

    /// <summary>
    /// Result of a handoff escalation check.
    /// </summary>
    public record HandoffCheckResult(
        bool ShouldEscalate,
        double Confidence,
        string? MatchType = null,
        string? Reason = null)
    {
        public static readonly HandoffCheckResult NoMatch = new(false, 0.0);
    }
}
