using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
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
                    r.Response,
                    r.Priority,
                    r.IsActive,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(rules);
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
                Trigger = dto.Trigger,
                Response = dto.Response,
                Priority = dto.Priority,
                IsActive = dto.IsActive
            };

            _db.ConversationRules.Add(rule);
            await _db.SaveChangesAsync();

            return Ok(new { rule.Id, rule.Type, rule.Trigger, rule.Response, rule.Priority, rule.IsActive, rule.CreatedAt });
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
            if (dto.Response != null) rule.Response = dto.Response;
            if (dto.Priority.HasValue) rule.Priority = dto.Priority.Value;
            if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;

            await _db.SaveChangesAsync();
            return Ok(new { rule.Id, rule.Type, rule.Trigger, rule.Response, rule.Priority, rule.IsActive, rule.CreatedAt });
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

            return Ok(new { matched = result != null, response = result });
        }
    }

    public class CreateRuleDto
    {
        public string Type { get; set; } = "keyword";
        public string Trigger { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }

    public class UpdateRuleDto
    {
        public string? Type { get; set; }
        public string? Trigger { get; set; }
        public string? Response { get; set; }
        public int? Priority { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TestRuleDto
    {
        public string Message { get; set; } = string.Empty;
    }
}
