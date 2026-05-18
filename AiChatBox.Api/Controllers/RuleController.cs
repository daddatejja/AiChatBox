using System.Security.Claims;
using System.Text.Json;
using System.Net.Http;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/rules")]
    public class RuleController(ChatDbContext db) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [AllowAnonymous]
        [HttpGet("test-anonymous")]
        public async Task<IActionResult> TestAnonymous()
        {
            var rules = await _db.ConversationRules.ToListAsync();
            var configs = await _db.Configurations.ToListAsync();
            var encryption = HttpContext.RequestServices.GetRequiredService<EncryptionService>();
            var decryptedKeys = configs.Select(c => new {
                c.Name,
                GeminiKey = string.IsNullOrEmpty(c.GeminiApiKey) ? null : encryption.Decrypt(c.GeminiApiKey),
                GroqKey = string.IsNullOrEmpty(c.GroqApiKey) ? null : encryption.Decrypt(c.GroqApiKey),
            }).ToList();
            return Ok(new { rules, configs, decryptedKeys });
        }

        /// <summary>List all rules for a project.</summary>
        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetRules(Guid projectId)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var rules = await _db.ConversationRules
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.Priority)
                .ThenBy(r => r.CreatedAt)
                .Select(r => new
                {
                    r.Id,
                    r.Type,
                    r.Trigger,
                    r.IntentLabel,
                    r.CommandName,
                    r.CommandTriggerChar,
                    r.CommandDescription,
                    r.ResponseType,
                    r.ResponsePayload,
                    r.Response,
                    r.ConfidenceThreshold,
                    r.Priority,
                    r.IsActive,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(rules);
        }

        /// <summary>
        /// Returns available commands for the widget autocomplete popup.
        /// Anonymous — called from the chatbox widget when a user types a trigger character.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("project/{projectId}/commands")]
        public async Task<IActionResult> GetCommands(Guid projectId, [FromQuery] string? triggerChar = null)
        {
            var ruleEngine = HttpContext.RequestServices.GetRequiredService<Services.RuleEngine>();
            var commands = await ruleEngine.GetCommandsAsync(projectId, triggerChar, HttpContext.RequestAborted);
            return Ok(commands);
        }

        /// <summary>Create a new rule.</summary>
        [HttpPost("project/{projectId}")]
        public async Task<IActionResult> CreateRule(Guid projectId, [FromBody] CreateRuleDto dto)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var rule = new ConversationRule
            {
                ProjectId = projectId,
                Type = dto.Type,
                Trigger = dto.Trigger ?? string.Empty,
                IntentLabel = dto.IntentLabel,
                CommandName = dto.CommandName,
                CommandTriggerChar = dto.CommandTriggerChar ?? "/",
                CommandDescription = dto.CommandDescription,
                ResponseType = dto.ResponseType ?? "text",
                ResponsePayload = dto.ResponsePayload,
                Response = dto.Response,
                ConfidenceThreshold = dto.ConfidenceThreshold,
                Priority = dto.Priority,
                IsActive = dto.IsActive
            };

            _db.ConversationRules.Add(rule);
            await _db.SaveChangesAsync();

            return Ok(MapRule(rule));
        }

        /// <summary>Update an existing rule.</summary>
        [HttpPut("{ruleId}")]
        public async Task<IActionResult> UpdateRule(Guid ruleId, [FromBody] UpdateRuleDto dto)
        {
            var rule = await _db.ConversationRules
                .Include(r => r.Project)
                .FirstOrDefaultAsync(r => r.Id == ruleId && r.Project!.UserId == UserId);

            if (rule == null) return NotFound();

            if (dto.Type != null) rule.Type = dto.Type;
            if (dto.Trigger != null) rule.Trigger = dto.Trigger;
            if (dto.IntentLabel != null) rule.IntentLabel = string.IsNullOrEmpty(dto.IntentLabel) ? null : dto.IntentLabel;
            if (dto.CommandName != null) rule.CommandName = string.IsNullOrEmpty(dto.CommandName) ? null : dto.CommandName;
            if (dto.CommandTriggerChar != null) rule.CommandTriggerChar = dto.CommandTriggerChar;
            if (dto.CommandDescription != null) rule.CommandDescription = string.IsNullOrEmpty(dto.CommandDescription) ? null : dto.CommandDescription;
            if (dto.ResponseType != null) rule.ResponseType = dto.ResponseType;
            if (dto.ResponsePayload != null) rule.ResponsePayload = string.IsNullOrEmpty(dto.ResponsePayload) ? null : dto.ResponsePayload;
            if (dto.Response != null) rule.Response = dto.Response;
            if (dto.ConfidenceThreshold.HasValue) rule.ConfidenceThreshold = dto.ConfidenceThreshold.Value;
            if (dto.Priority.HasValue) rule.Priority = dto.Priority.Value;
            if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return Ok(MapRule(rule));
        }

        /// <summary>Delete a rule.</summary>
        [HttpDelete("{ruleId}")]
        public async Task<IActionResult> DeleteRule(Guid ruleId)
        {
            var rule = await _db.ConversationRules
                .Include(r => r.Project)
                .FirstOrDefaultAsync(r => r.Id == ruleId && r.Project!.UserId == UserId);

            if (rule == null) return NotFound();

            _db.ConversationRules.Remove(rule);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>Test a message against project rules without sending to LLM.</summary>
        [HttpPost("project/{projectId}/test")]
        public async Task<IActionResult> TestRule(Guid projectId, [FromBody] TestRuleDto dto)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var ruleEngine = HttpContext.RequestServices.GetRequiredService<Services.RuleEngine>();
            var result = await ruleEngine.TryMatchAsync(projectId, dto.Message);

            return Ok(new {
                matched = result != null,
                response = result?.Response,
                matchType = result?.MatchType,
                confidence = result?.Confidence,
                intentLabel = result?.IntentLabel,
                responseType = result?.ResponseType,
                responsePayload = result?.ResponsePayload
            });
        }

        [AllowAnonymous]
        [HttpPost("project/{projectId}/test-anonymous")]
        public async Task<IActionResult> TestRuleAnonymous(Guid projectId, [FromBody] TestRuleDto dto)
        {
            var ruleEngine = HttpContext.RequestServices.GetRequiredService<Services.RuleEngine>();
            var result = await ruleEngine.TryMatchAsync(projectId, dto.Message);

            return Ok(new {
                matched = result != null,
                response = result?.Response,
                matchType = result?.MatchType,
                confidence = result?.Confidence,
                intentLabel = result?.IntentLabel,
                responseType = result?.ResponseType,
                responsePayload = result?.ResponsePayload
            });
        }

        [AllowAnonymous]
        [HttpPost("form-submit")]
        public async Task<IActionResult> SubmitForm([FromBody] FormSubmitDto dto)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == dto.ProjectId);
            if (project == null) return NotFound("Project not found.");

            var targetUrl = !string.IsNullOrEmpty(dto.SubmitUrl) ? dto.SubmitUrl : project.WebhookUrl;

            if (string.IsNullOrEmpty(targetUrl))
            {
                return BadRequest("Submission URL is not configured (neither a custom form webhook/submit URL nor a project-level webhook URL is set).");
            }

            try
            {
                var client = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                
                var webhookPayload = new
                {
                    @event = "form_submission",
                    projectId = dto.ProjectId,
                    projectName = project.Name,
                    sessionId = dto.SessionId,
                    formTitle = dto.FormTitle,
                    submittedData = dto.Data,
                    timestamp = DateTime.UtcNow
                };

                var payloadJson = JsonSerializer.Serialize(webhookPayload);
                var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");

                if (!string.IsNullOrEmpty(project.WebhookSecret))
                {
                    using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(project.WebhookSecret));
                    var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payloadJson));
                    var signature = Convert.ToHexString(hash).ToLower();
                    content.Headers.Add("X-Hub-Signature", signature);
                }

                var response = await client.PostAsync(targetUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    return StatusCode((int)response.StatusCode, $"Customer webhook failed: {errorMsg}");
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal submission error: {ex.Message}");
            }
        }

        private static object MapRule(ConversationRule rule) => new
        {
            rule.Id,
            rule.Type,
            rule.Trigger,
            rule.IntentLabel,
            rule.CommandName,
            rule.CommandTriggerChar,
            rule.CommandDescription,
            rule.ResponseType,
            rule.ResponsePayload,
            rule.Response,
            rule.ConfidenceThreshold,
            rule.Priority,
            rule.IsActive,
            rule.CreatedAt
        };
    }

    public class CreateRuleDto
    {
        public string Type { get; set; } = "keyword";
        public string? Trigger { get; set; }
        public string? IntentLabel { get; set; }
        // Command fields
        public string? CommandName { get; set; }
        public string? CommandTriggerChar { get; set; } = "/";
        public string? CommandDescription { get; set; }
        // Response fields
        public string? ResponseType { get; set; } = "text";
        public string? ResponsePayload { get; set; }
        public string Response { get; set; } = string.Empty;
        public double ConfidenceThreshold { get; set; } = 0.75;
        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateRuleDto
    {
        public string? Type { get; set; }
        public string? Trigger { get; set; }
        public string? IntentLabel { get; set; }
        // Command fields
        public string? CommandName { get; set; }
        public string? CommandTriggerChar { get; set; }
        public string? CommandDescription { get; set; }
        // Response fields
        public string? ResponseType { get; set; }
        public string? ResponsePayload { get; set; }
        public string? Response { get; set; }
        public double? ConfidenceThreshold { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TestRuleDto
    {
        public string Message { get; set; } = string.Empty;
    }

    public class FormSubmitDto
    {
        public Guid ProjectId { get; set; }
        public string? SessionId { get; set; }
        public string? FormTitle { get; set; }
        public string? SubmitUrl { get; set; }
        public Dictionary<string, string>? Data { get; set; }
    }
}
