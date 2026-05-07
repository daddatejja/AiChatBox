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
    [Route("api/[controller]")]
    public class ToolController(ChatDbContext db) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
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
    }
}
