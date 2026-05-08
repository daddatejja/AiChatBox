using System.Security.Claims;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetSessions()
        {
            var sessions = await _chatService.GetSessionsAsync(UserId);
            return Ok(sessions);
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchivedSessions()
        {
            var sessions = await _chatService.GetArchivedSessionsAsync(UserId);
            return Ok(sessions);
        }

        [HttpGet("sessions/{sessionId:guid}")]
        public async Task<IActionResult> GetMessages(Guid sessionId)
        {
            var messages = await _chatService.GetSessionMessagesAsync(sessionId, UserId);
            return Ok(messages);
        }

        [HttpPost("sessions/{sessionId:guid}/archive")]
        public async Task<IActionResult> ArchiveSession(Guid sessionId)
        {
            var result = await _chatService.ArchiveSessionAsync(sessionId, UserId);
            return result ? Ok() : NotFound();
        }

        [HttpDelete("sessions/{sessionId:guid}/hard")]
        public async Task<IActionResult> HardDeleteSession(Guid sessionId)
        {
            var result = await _chatService.HardDeleteSessionAsync(sessionId, UserId);
            return result ? Ok() : NotFound();
        }
    }
}
