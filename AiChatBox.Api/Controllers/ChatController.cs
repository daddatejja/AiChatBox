using System.Security.Claims;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiKeyOrJwt")]
    public class ChatController(IAiChatService chatService) : ControllerBase
    {
        private readonly IAiChatService _chatService = chatService;

        private string UserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            Request.Headers["X-User-Id"].ToString() ??
            (HttpContext.Items["CurrentProject"] is Project p ? $"project-{p.Id}" : "standalone-user");

        private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };

        [HttpPost]
        public async Task Post([FromBody] ChatRequest request)
        {
            if (!ModelState.IsValid)
            {
                Response.ContentType = "text/event-stream";
                var errorChunk = new ChatStreamChunk { Error = "Invalid request" };
                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(errorChunk, _jsonOptions)}\n\n");
                await Response.Body.FlushAsync();
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";

            try
            {
                await foreach (var chunk in _chatService.StreamChatAsync(request, UserId, HttpContext.RequestAborted))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(chunk, _jsonOptions);
                    await Response.WriteAsync($"data: {json}\n\n");
                    await Response.Body.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                var errorChunk = new ChatStreamChunk { Error = ex.Message };
                var json = System.Text.Json.JsonSerializer.Serialize(errorChunk, _jsonOptions);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }
        }

        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] Guid? projectId)
        {
            var sessions = await _chatService.GetSessionsAsync(UserId, projectId);
            return Ok(sessions);
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchivedSessions([FromQuery] Guid? projectId)
        {
            var sessions = await _chatService.GetArchivedSessionsAsync(UserId, projectId);
            return Ok(sessions);
        }

        [HttpGet("sessions/{sessionId:guid}")]
        public async Task<IActionResult> GetMessages(Guid sessionId, [FromQuery] Guid? projectId)
        {
            var messages = await _chatService.GetSessionMessagesAsync(sessionId, UserId, projectId);
            return Ok(messages);
        }

        [HttpPost("sessions/{sessionId:guid}/archive")]
        public async Task<IActionResult> ArchiveSession(Guid sessionId, [FromQuery] Guid? projectId)
        {
            var result = await _chatService.ArchiveSessionAsync(sessionId, UserId, projectId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("sessions/{sessionId:guid}/hard")]
        public async Task<IActionResult> HardDeleteSession(Guid sessionId, [FromQuery] Guid? projectId)
        {
            var result = await _chatService.HardDeleteSessionAsync(sessionId, UserId, projectId);
            return result ? Ok() : NotFound();
        }
        
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var project = HttpContext.Items["CurrentProject"] as Project;
            var config = HttpContext.Items["CurrentConfiguration"] as ProjectConfiguration;

            if (project == null) return Unauthorized(new { error = "Project not found or API key missing" });

            var defaultProvider = config?.DefaultProvider ?? project.Provider;
            var enabledModels = new List<DTOs.ModelOptionDto>();
            var rawModels = config?.EnabledModels;

            if (!string.IsNullOrEmpty(rawModels))
            {
                var trimmed = rawModels.Trim();
                if (trimmed.StartsWith("["))
                {
                    // Try parsing as JSON array of {model, provider} objects first
                    try
                    {
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<DTOs.ModelOptionDto>>(trimmed, 
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (parsed?.Count > 0 && !string.IsNullOrEmpty(parsed[0].Model))
                        {
                            enabledModels = parsed;
                        }
                    }
                    catch { }

                    // Fallback: try parsing as JSON array of plain strings
                    if (enabledModels.Count == 0)
                    {
                        try
                        {
                            var strings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(trimmed);
                            enabledModels = strings?.Select(m => new DTOs.ModelOptionDto 
                            { 
                                Model = m, 
                                Provider = defaultProvider 
                            }).ToList() ?? [];
                        }
                        catch { }
                    }
                }

                // Fallback: comma-separated plain strings
                if (enabledModels.Count == 0)
                {
                    enabledModels = rawModels.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => new DTOs.ModelOptionDto 
                        { 
                            Model = m.Trim(), 
                            Provider = defaultProvider 
                        }).ToList();
                }
            }

            object? theme = null;
            if (!string.IsNullOrEmpty(config?.ThemeSettingsJson))
            {
                try { theme = System.Text.Json.JsonSerializer.Deserialize<object>(config.ThemeSettingsJson); }
                catch { }
            }

            return Ok(new DTOs.ChatConfigDto
            {
                ProjectName = project.Name,
                DefaultProvider = defaultProvider,
                DefaultModel = config?.DefaultModel ?? project.ModelName,
                LiveVoiceEnabled = config?.LiveVoiceEnabled ?? false,
                EnabledModels = enabledModels,
                Suggestions = !string.IsNullOrEmpty(config?.SuggestionsJson) 
                    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(config.SuggestionsJson) ?? [] 
                    : [],
                SystemPrompt = config?.SystemPrompt ?? project.SystemPrompt,
                HandoffEnabled = config?.HandoffEnabled ?? false,
                Theme = theme
            });
        }

        /// <summary>Submit feedback on a chat message (1 = thumbs up, -1 = thumbs down).</summary>
        [HttpPost("messages/{messageId}/feedback")]
        public async Task<IActionResult> SubmitFeedback(Guid messageId, [FromBody] FeedbackDto dto)
        {
            var db = HttpContext.RequestServices.GetRequiredService<Data.ChatDbContext>();
            var message = await db.ChatMessages
                .Include(m => m.Session)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (message == null) return NotFound();

            message.Feedback = dto.Feedback;
            await db.SaveChangesAsync();

            return Ok(new { messageId = message.Id, feedback = message.Feedback });
        }

        /// <summary>
        /// Poll for new messages (used by widget during active human handoff).
        /// </summary>
        [HttpGet("{sessionId}/poll")]
        public async Task<IActionResult> PollMessages(Guid sessionId, [FromQuery] string since)
        {
            var db = HttpContext.RequestServices.GetRequiredService<Data.ChatDbContext>();
            
            // Basic validation that session exists and belongs to project
            var project = HttpContext.Items["CurrentProject"] as Project;
            if (project == null) return Unauthorized();

            var session = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.ProjectId == project.Id);
            if (session == null) return NotFound("Session not found");

            // We only need to return agent messages or system messages that are newer than 'since'
            if (!DateTime.TryParse(since, out var sinceDate))
            {
                sinceDate = DateTime.UtcNow.AddMinutes(-5); // fallback
            }

            var messages = await db.ChatMessages
                .Where(m => m.SessionId == sessionId && m.CreatedAt > sinceDate && m.Role != "user")
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    id = m.Id,
                    role = m.Role,
                    content = m.Content,
                    createdAt = m.CreatedAt
                })
                .ToListAsync();

            return Ok(new {
                handoffStatus = session.HandoffStatus,
                messages,
                serverTime = DateTime.UtcNow.ToString("o")
            });
        }
    }

    public class FeedbackDto
    {
        /// <summary>1 = thumbs up, -1 = thumbs down, null = clear feedback.</summary>
        public int? Feedback { get; set; }
    }
}
