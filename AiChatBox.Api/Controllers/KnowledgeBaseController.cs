using System.Security.Claims;
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
    [Route("api/project/{projectId}/knowledge")]
    public class KnowledgeBaseController(ChatDbContext db, FileProcessingService fileService) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly FileProcessingService _fileService = fileService;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<ActionResult> GetDocuments(Guid projectId)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!project) return NotFound();

            var docs = await _db.KnowledgeDocuments
                .Where(d => d.ProjectId == projectId)
                .OrderByDescending(d => d.Id)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.ContentType,
                    d.FileSize,
                    d.IsProcessed,
                    ChunkCount = d.Chunks.Count
                })
                .ToListAsync();

            return Ok(docs);
        }

        [HttpPost("upload")]
        public async Task<ActionResult> UploadDocument(Guid projectId, IFormFile file)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!project) return NotFound();

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            using var stream = file.OpenReadStream();
            var doc = await _fileService.ProcessKnowledgeDocumentAsync(projectId, stream, file.FileName, file.ContentType);

            return Ok(new
            {
                doc.Id,
                doc.FileName,
                doc.IsProcessed
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid projectId, Guid id)
        {
            var doc = await _db.KnowledgeDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.ProjectId == projectId && d.Project!.UserId == UserId);

            if (doc == null) return NotFound();

            _db.KnowledgeDocuments.Remove(doc);
            await _db.SaveChangesAsync();

            return NoContent();
        }
    }
}
