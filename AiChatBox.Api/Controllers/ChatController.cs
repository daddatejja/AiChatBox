using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController(IAiChatService chatService) : ControllerBase
    {
        private readonly IAiChatService _chatService = chatService;

        private string UserId => Request.Headers["X-User-Id"].ToString() ?? "standalone-user";

        [HttpPost]
        public async Task Post([FromBody] ChatRequest request)
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            await foreach (var chunk in _chatService.StreamChatAsync(request, UserId, HttpContext.RequestAborted))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(chunk);
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
