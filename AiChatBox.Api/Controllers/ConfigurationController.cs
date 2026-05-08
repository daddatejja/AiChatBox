using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class ConfigurationController(ChatDbContext db) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet("project/{projectId}/configurations")]
        public async Task<ActionResult<IEnumerable<ConfigurationDto>>> GetConfigurations(Guid projectId)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!project) return NotFound();

            var configs = await _db.Configurations
                .Where(c => c.ProjectId == projectId)
                .Select(c => new ConfigurationDto
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Name = c.Name,
                    SystemPrompt = c.SystemPrompt,
                    DefaultProvider = c.DefaultProvider,
                    DefaultModel = c.DefaultModel,
                    LiveVoiceEnabled = c.LiveVoiceEnabled,
                    HasGeminiKey = c.GeminiApiKey != null,
                    HasGroqKey = c.GroqApiKey != null,
                    HasOpenAiKey = c.OpenAiApiKey != null,
                    CreatedAt = c.CreatedAt,
                    ApiKeyCount = c.ApiKeys.Count
                })
                .ToListAsync();

            return Ok(configs);
        }

        [HttpGet("configuration/{id}")]
        public async Task<ActionResult<ConfigurationDetailDto>> GetConfiguration(Guid id)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            return Ok(new ConfigurationDetailDto
            {
                Id = config.Id,
                ProjectId = config.ProjectId,
                Name = config.Name,
                SystemPrompt = config.SystemPrompt,
                GeminiApiKey = config.GeminiApiKey,
                GroqApiKey = config.GroqApiKey,
                OpenAiApiKey = config.OpenAiApiKey,
                DefaultProvider = config.DefaultProvider,
                DefaultModel = config.DefaultModel,
                LiveVoiceEnabled = config.LiveVoiceEnabled,
                CreatedAt = config.CreatedAt,
                EnabledModels = config.EnabledModels
            });
        }

        [HttpPost("project/{projectId}/configurations")]
        public async Task<ActionResult<ConfigurationDto>> CreateConfiguration(Guid projectId, CreateConfigurationDto model)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var config = new ProjectConfiguration
            {
                ProjectId = projectId,
                Name = model.Name,
                SystemPrompt = model.SystemPrompt,
                DefaultProvider = model.DefaultProvider,
                DefaultModel = model.DefaultModel
            };

            _db.Configurations.Add(config);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetConfiguration), new { id = config.Id }, new ConfigurationDto
            {
                Id = config.Id,
                ProjectId = config.ProjectId,
                Name = config.Name,
                SystemPrompt = config.SystemPrompt,
                DefaultProvider = config.DefaultProvider,
                DefaultModel = config.DefaultModel,
                LiveVoiceEnabled = config.LiveVoiceEnabled,
                CreatedAt = config.CreatedAt
            });
        }

        [HttpPut("configuration/{id}")]
        public async Task<IActionResult> UpdateConfiguration(Guid id, UpdateConfigurationDto model)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            if (model.Name != null) config.Name = model.Name;
            if (model.SystemPrompt != null) config.SystemPrompt = model.SystemPrompt;
            if (model.GeminiApiKey != null) config.GeminiApiKey = model.GeminiApiKey == "" ? null : model.GeminiApiKey;
            if (model.GroqApiKey != null) config.GroqApiKey = model.GroqApiKey == "" ? null : model.GroqApiKey;
            if (model.OpenAiApiKey != null) config.OpenAiApiKey = model.OpenAiApiKey == "" ? null : model.OpenAiApiKey;
            if (model.DefaultProvider != null) config.DefaultProvider = model.DefaultProvider;
            if (model.DefaultModel != null) config.DefaultModel = model.DefaultModel;
            if (model.LiveVoiceEnabled.HasValue) config.LiveVoiceEnabled = model.LiveVoiceEnabled.Value;
            if (model.EnabledModels != null) config.EnabledModels = model.EnabledModels;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("configuration/{id}")]
        public async Task<IActionResult> DeleteConfiguration(Guid id)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            _db.Configurations.Remove(config);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
