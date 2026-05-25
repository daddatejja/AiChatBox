using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/channel")]
    public class ChannelController(
        IDbContextFactory<ChatDbContext> dbFactory,
        IEnumerable<IChannelAdapter> adapters,
        IAiChatService chatService,
        IHubContext<LiveChatHub> hubContext,
        EncryptionService encryptionService,
        IServiceScopeFactory scopeFactory,
        ILogger<ChannelController> logger) : ControllerBase
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly IEnumerable<IChannelAdapter> _adapters = adapters;
        private readonly IAiChatService _chatService = chatService;
        private readonly IHubContext<LiveChatHub> _hubContext = hubContext;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly ILogger<ChannelController> _logger = logger;

        private IChannelAdapter GetAdapter(string channel)
        {
            var adapter = _adapters.FirstOrDefault(a => a.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));
            if (adapter == null)
                throw new NotSupportedException($"Channel '{channel}' is not supported.");
            return adapter;
        }

        // ─── WhatsApp Webhook Verification Handshake ───
        [HttpGet("whatsapp/{projectId:guid}")]
        public async Task<IActionResult> VerifyWhatsApp(
            Guid projectId,
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.challenge")] string challenge,
            [FromQuery(Name = "hub.verify_token")] string verifyToken)
        {
            if (mode != "subscribe") return BadRequest("Invalid mode.");

            await using var db = await _dbFactory.CreateDbContextAsync();
            var config = await db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Name == "Default");
            if (config == null)
            {
                config = await db.Configurations.FirstOrDefaultAsync(c => c.ProjectId == projectId);
            }
            
            if (config == null || string.IsNullOrWhiteSpace(config.ChannelSettingsJson))
                return Unauthorized("Verification failed: Project has no configuration configured.");

            var settings = JsonSerializer.Deserialize<ChannelSettings>(config.ChannelSettingsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (settings?.WhatsApp == null || string.IsNullOrWhiteSpace(settings.WhatsApp.VerifyToken))
                return Unauthorized("Verification failed: WhatsApp verify token is not configured.");

            if (verifyToken == settings.WhatsApp.VerifyToken)
            {
                return Ok(challenge);
            }

            return Unauthorized("Verification token mismatch.");
        }

        // ─── Unified POST webhook handler for WhatsApp, Slack, Telegram ───
        [HttpPost("{channel}/{projectId:guid}")]
        public async Task<IActionResult> HandleWebhook(string channel, Guid projectId)
        {
            try
            {
                var adapter = GetAdapter(channel);
                var inbound = await adapter.ParseInbound(Request);

                // Check for Slack URL verification handshake
                if (channel.Equals("slack", StringComparison.OrdinalIgnoreCase) && 
                    inbound.SenderId == "slack_system" && 
                    inbound.SessionExternalId == "handshake")
                {
                    return Ok(new { challenge = inbound.Text });
                }

                // Protect against bot self-loops and empty inputs
                if (inbound.SenderId == "bot" || string.IsNullOrWhiteSpace(inbound.Text))
                {
                    return Ok();
                }

                await using var db = await _dbFactory.CreateDbContextAsync();

                // 1. Look up or create the ChatSession mapped to this external channel user
                var session = await db.ChatSessions
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(s => s.ExternalSenderId == inbound.SenderId && s.ProjectId == projectId);

                if (session == null)
                {
                    var sessionTitle = !string.IsNullOrEmpty(inbound.SenderName)
                        ? $"{inbound.SenderName} ({channel})"
                        : $"Session: {inbound.SenderId} ({channel})";

                    session = new ChatSession
                    {
                        ExternalSenderId = inbound.SenderId,
                        ProjectId = projectId,
                        UserId = $"external-{channel}-{inbound.SenderId}",
                        Title = sessionTitle,
                        CreatedAt = DateTime.UtcNow,
                        LastMessageAt = DateTime.UtcNow,
                        HandoffStatus = "ai"
                    };
                    db.ChatSessions.Add(session);
                    await db.SaveChangesAsync();
                }
                else if (!string.IsNullOrEmpty(inbound.SenderName) && (string.IsNullOrEmpty(session.Title) || session.Title.StartsWith("Session: ")))
                {
                    session.Title = $"{inbound.SenderName} ({channel})";
                    await db.SaveChangesAsync();
                }

                // 2. Save the incoming message from the user
                var msgContent = inbound.Text;
                if (!string.IsNullOrEmpty(inbound.AttachmentUrl))
                {
                    msgContent += $"\n\n📎 Attachment: {inbound.AttachmentUrl}";
                }

                var userMsg = new ChatMessage
                {
                    SessionId = session.Id,
                    Role = "user",
                    Content = msgContent,
                    ImageDataUrl = !string.IsNullOrEmpty(inbound.AttachmentUrl) && 
                                   (inbound.AttachmentUrl.Contains(".png") || inbound.AttachmentUrl.Contains(".jpg") || inbound.AttachmentUrl.Contains(".jpeg") || inbound.AttachmentUrl.Contains(".webp") || inbound.AttachmentUrl.Contains("google.com") || inbound.AttachmentUrl.Contains("telegram.org")) 
                                   ? inbound.AttachmentUrl 
                                   : null,
                    CreatedAt = DateTime.UtcNow
                };
                db.ChatMessages.Add(userMsg);
                await db.SaveChangesAsync();

                // 3. If human handoff is active, redirect message to agent via SignalR
                if (session.HandoffStatus == "active")
                {
                    await _hubContext.Clients.Group($"session-{session.Id}").SendAsync("ReceiveUserMessage", new
                    {
                        id = userMsg.Id,
                        sessionId = session.Id,
                        content = userMsg.Content,
                        createdAt = userMsg.CreatedAt,
                        role = "user"
                    });
                    return Ok();
                }

                // 4. Generate AI Agent response & Send reply outbound back over the channel asynchronously (in background)
                // This returns a 200 OK immediately, preventing webhook timeout errors from third-party channels (like OpenWA).
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scopedChatService = scope.ServiceProvider.GetRequiredService<IAiChatService>();
                        var scopedAdapters = scope.ServiceProvider.GetServices<IChannelAdapter>();
                        var scopedAdapter = scopedAdapters.FirstOrDefault(a => a.ChannelName.Equals(channel, StringComparison.OrdinalIgnoreCase));

                        if (scopedAdapter == null)
                        {
                            _logger.LogError("Scoped adapter not found for channel {Channel} in background processing.", channel);
                            return;
                        }

                        var responseText = new StringBuilder();
                        var chatRequest = new ChatRequest
                        {
                            SessionId = session.Id,
                            Message = inbound.Text,
                            ProjectId = projectId
                        };

                        await foreach (var chunk in scopedChatService.StreamChatAsync(chatRequest, session.UserId, default))
                        {
                            if (!string.IsNullOrEmpty(chunk.Text))
                            {
                                responseText.Append(chunk.Text);
                            }
                            if (!string.IsNullOrEmpty(chunk.Error))
                            {
                                responseText.Append($" [Error: {chunk.Error}]");
                            }
                        }

                        var replyText = responseText.ToString();

                        var outbound = new OutboundMessage
                        {
                            RecipientId = inbound.SessionExternalId ?? inbound.SenderId,
                            Text = replyText,
                            Channel = channel,
                            SessionId = session.Id,
                            ProjectId = projectId
                        };

                        await scopedAdapter.SendOutbound(outbound);
                    }
                    catch (Exception bgEx)
                    {
                        _logger.LogError(bgEx, "Background processing for webhook message failed (Channel: {Channel}, SessionId: {SessionId})", channel, session.Id);
                    }
                });

                return Ok();
            }
            catch (NotSupportedException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Webhook Error] {Channel} failed to process", channel);
                return StatusCode(500, $"Internal Webhook Error: {ex.Message}");
            }
        }
    }
}
