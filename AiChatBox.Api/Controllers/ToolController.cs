using System.Security.Claims;
using System.Diagnostics;
using System.Text.Json;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using AiChatBox.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ToolController(ChatDbContext db, WebhookService webhookService, IHttpClientFactory httpClientFactory) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly WebhookService _webhookService = webhookService;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet("project/{projectId}")]
        public async Task<ActionResult<IEnumerable<CustomToolDto>>> GetProjectTools(Guid projectId)
        {
            var isOwner = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!isOwner) return Forbid();

            var tools = await _db.CustomTools
                .Where(t => t.ProjectId == projectId)
                .Select(t => new CustomToolDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    ParametersJsonSchema = t.ParametersJsonSchema,
                    IsActive = t.IsActive
                })
                .ToListAsync();

            return tools;
        }

        [HttpPost("project/{projectId}")]
        public async Task<ActionResult<CustomToolDto>> CreateTool(Guid projectId, CustomToolDto model)
        {
            var isOwner = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!isOwner) return Forbid();

            var tool = new CustomTool
            {
                ProjectId = projectId,
                Name = model.Name,
                Description = model.Description,
                ParametersJsonSchema = model.ParametersJsonSchema,
                IsActive = true
            };

            _db.CustomTools.Add(tool);
            await _db.SaveChangesAsync();

            model.Id = tool.Id;
            return Ok(model);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTool(Guid id, CustomToolDto model)
        {
            var tool = await _db.CustomTools
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id && t.Project!.UserId == UserId);

            if (tool == null) return NotFound();

            tool.Name = model.Name;
            tool.Description = model.Description;
            tool.ParametersJsonSchema = model.ParametersJsonSchema;
            tool.IsActive = model.IsActive;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTool(Guid id)
        {
            var tool = await _db.CustomTools
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id && t.Project!.UserId == UserId);

            if (tool == null) return NotFound();

            _db.CustomTools.Remove(tool);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("project/{projectId}/test-webhook-connection")]
        public async Task<ActionResult<WebhookTestResultDto>> TestWebhookConnection(Guid projectId, [FromBody] TestWebhookConnectionRequest request)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var url = !string.IsNullOrEmpty(request.WebhookUrl) ? request.WebhookUrl : project.WebhookUrl;
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("Webhook URL not configured.");
            }

            var secret = request.WebhookSecret;
            if (string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(project.WebhookSecret))
            {
                secret = project.WebhookSecret;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10); // Don't hang forever
                
                var payload = new
                {
                    ProjectName = project.Name,
                    Event = "ping",
                    Message = "Webhook test connection"
                };

                var payloadJson = JsonSerializer.Serialize(payload);
                var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

                // Sign if secret configured
                if (!string.IsNullOrEmpty(secret))
                {
                    content.Headers.Add("X-Hub-Signature", ComputeSignature(secret, payloadJson));
                }

                var response = await client.PostAsync(url, content);
                stopwatch.Stop();

                var responseBody = await response.Content.ReadAsStringAsync();

                return Ok(new WebhookTestResultDto
                {
                    StatusCode = (int)response.StatusCode,
                    ResponseBody = responseBody,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Success = response.IsSuccessStatusCode
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return Ok(new WebhookTestResultDto
                {
                    StatusCode = 0,
                    ResponseBody = $"Error connecting to webhook: {ex.Message}",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Success = false
                });
            }
        }

        [HttpPost("{id}/execute")]
        public async Task<ActionResult<ToolResult>> ExecuteTool(Guid id, [FromBody] ExecuteToolRequest request)
        {
            var tool = await _db.CustomTools
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Id == id && t.Project!.UserId == UserId);

            if (tool == null) return NotFound();

            if (string.IsNullOrEmpty(tool.Project!.WebhookUrl))
            {
                return BadRequest("Webhook URL not configured for this project.");
            }

            var result = await _webhookService.ExecuteWebhookToolAsync(tool.Project, tool.Name, request.ArgumentsJson, tool.ParametersJsonSchema);
            return Ok(result);
        }

        private string ComputeSignature(string secret, string payload)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLower();
        }
    }
}
