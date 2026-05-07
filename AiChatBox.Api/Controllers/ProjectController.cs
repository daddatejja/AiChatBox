using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController(ChatDbContext db, ApiKeyService apiKeyService) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly ApiKeyService _apiKeyService = apiKeyService;

        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectDto>>> GetProjects()
        {
            var projects = await _db.Projects
                .Where(p => p.UserId == UserId)
                .Include(p => p.ApiKeys)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new ProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SystemPrompt = p.SystemPrompt,
                    Provider = p.Provider,
                    ModelName = p.ModelName,
                    WebhookUrl = p.WebhookUrl,
                    CreatedAt = p.CreatedAt,
                    ApiKeyCount = p.ApiKeys.Count
                })
                .ToListAsync();

            return projects;
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> CreateProject(CreateProjectDto model)
        {
            var project = new Project
            {
                UserId = UserId,
                Name = model.Name,
                SystemPrompt = model.SystemPrompt,
                Provider = model.Provider,
                ModelName = model.ModelName
            };

            _db.Projects.Add(project);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                SystemPrompt = project.SystemPrompt,
                Provider = project.Provider,
                ModelName = project.ModelName,
                CreatedAt = project.CreatedAt
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ProjectDto>> GetProject(Guid id)
        {
            var project = await _db.Projects
                .Include(p => p.ApiKeys)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == UserId);

            if (project == null) return NotFound();

            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                SystemPrompt = project.SystemPrompt,
                Provider = project.Provider,
                ModelName = project.ModelName,
                WebhookUrl = project.WebhookUrl,
                CreatedAt = project.CreatedAt,
                ApiKeyCount = project.ApiKeys.Count
            };
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(Guid id, UpdateProjectDto model)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == UserId);
            if (project == null) return NotFound();

            project.Name = model.Name;
            project.SystemPrompt = model.SystemPrompt;
            project.Provider = model.Provider;
            project.ModelName = model.ModelName;
            project.WebhookUrl = model.WebhookUrl;
            if (!string.IsNullOrEmpty(model.WebhookSecret))
                project.WebhookSecret = model.WebhookSecret;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id && p.UserId == UserId);
            if (project == null) return NotFound();

            _db.Projects.Remove(project);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // API Key Management
        [HttpPost("{id}/keys")]
        public async Task<ActionResult> CreateApiKey(Guid id, [FromBody] string label)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == id && p.UserId == UserId);
            if (!project) return NotFound();

            var (rawKey, entity) = await _apiKeyService.GenerateApiKeyAsync(id, label);
            return Ok(new { key = rawKey, label = entity.Label, createdAt = entity.CreatedAt });
        }

        [HttpGet("{id}/keys")]
        public async Task<ActionResult> GetApiKeys(Guid id)
        {
            var keys = await _db.ApiKeys
                .Where(k => k.ProjectId == id && k.Project!.UserId == UserId)
                .Select(k => new { k.Id, k.Label, k.CreatedAt, k.LastUsedAt, k.IsActive })
                .ToListAsync();

            return Ok(keys);
        }

        [HttpDelete("keys/{keyId}")]
        public async Task<IActionResult> DeleteApiKey(Guid keyId)
        {
            var key = await _db.ApiKeys
                .Include(k => k.Project)
                .FirstOrDefaultAsync(k => k.Id == keyId && k.Project!.UserId == UserId);

            if (key == null) return NotFound();

            _db.ApiKeys.Remove(key);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
