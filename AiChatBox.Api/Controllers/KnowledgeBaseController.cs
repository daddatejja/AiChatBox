using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/knowledge")]
    public class KnowledgeBaseController(ChatDbContext db, FileProcessingService fileService, EncryptionService encryptionService, IConfiguration configuration) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly FileProcessingService _fileService = fileService;
        private readonly EncryptionService _encryptionService = encryptionService;
        private readonly string _uploadBasePath = configuration["FileStorage:BasePath"] ?? Path.Combine("wwwroot", "uploads", "chat");
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
                    Status = d.Status.ToString(),
                    d.ErrorMessage,
                    ChunkCount = d.Chunks.Count,
                    d.CreatedAt
                })
                .ToListAsync();

            return Ok(docs);
        }

        [HttpPost("upload")]
        public async Task<ActionResult> UploadDocument(Guid projectId, IFormFile file)
        {
            var project = await _db.Projects
                .Include(p => p.Configurations)
                .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
                
            if (project == null) return NotFound();

            // Find any configuration with an API key (prioritizing Gemini for now)
            var config = project.Configurations.FirstOrDefault(c => !string.IsNullOrEmpty(c.GeminiApiKey));
            if (config == null)
            {
                return BadRequest("No project configuration with a Gemini API key found. Please create a configuration with an API key first.");
            }

            // Check Budget and Rate Limits
            if (config.MaxSpendLimit > 0 && config.CurrentSpend >= config.MaxSpendLimit)
            {
                return BadRequest("Budget limit reached for this configuration.");
            }

            if (config.RateLimitRequests > 0)
            {
                var windowStart = DateTime.UtcNow.AddMinutes(-config.RateLimitWindowMinutes);
                var requestCount = await _db.AiRequestLogs.CountAsync(l => l.ProjectId == projectId && l.CreatedAt > windowStart);
                if (requestCount >= config.RateLimitRequests)
                {
                    return StatusCode(429, "Rate limit exceeded for this project. Please try again later.");
                }
            }

            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            // Decrypt the API key before passing it to the processing service
            var decryptedKey = _encryptionService.Decrypt(config.GeminiApiKey);

            using var stream = file.OpenReadStream();
            var doc = await _fileService.ProcessKnowledgeDocumentAsync(projectId, stream, file.FileName, file.ContentType, decryptedKey);

            return Ok(new
            {
                doc.Id,
                doc.FileName,
                Status = doc.Status.ToString(),
                doc.IsProcessed,
                doc.ErrorMessage
            });
        }

        [HttpPost("crawl")]
        public async Task<ActionResult> StartCrawl(Guid projectId, [FromBody] CrawlRequest request)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!project) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Url)) return BadRequest("URL is required.");

            var job = new WebsiteCrawlJob
            {
                ProjectId = projectId,
                BaseUrl = request.Url,
                MaxPages = request.MaxPages ?? 10,
                Status = KnowledgeDocumentStatus.Pending
            };

            _db.WebsiteCrawlJobs.Add(job);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Crawl job started", JobId = job.Id });
        }

        public class CrawlRequest
        {
            public string Url { get; set; } = string.Empty;
            public int? MaxPages { get; set; }
        }

        [HttpPost("{id}/retry")]
        public async Task<ActionResult> RetryProcessing(Guid projectId, Guid id)
        {
            var doc = await _db.KnowledgeDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.ProjectId == projectId && d.Project!.UserId == UserId);

            if (doc == null) return NotFound();

            var project = await _db.Projects
                .Include(p => p.Configurations)
                .FirstOrDefaultAsync(p => p.Id == projectId);
            
            var config = project?.Configurations.FirstOrDefault(c => !string.IsNullOrEmpty(c.GeminiApiKey));
            if (config == null)
            {
                return BadRequest("No project configuration with a Gemini API key found.");
            }

            // Check Budget and Rate Limits
            if (config.MaxSpendLimit > 0 && config.CurrentSpend >= config.MaxSpendLimit)
            {
                return BadRequest("Budget limit reached for this configuration.");
            }

            if (config.RateLimitRequests > 0)
            {
                var windowStart = DateTime.UtcNow.AddMinutes(-config.RateLimitWindowMinutes);
                var requestCount = await _db.AiRequestLogs.CountAsync(l => l.ProjectId == projectId && l.CreatedAt > windowStart);
                if (requestCount >= config.RateLimitRequests)
                {
                    return StatusCode(429, "Rate limit exceeded for this project. Please try again later.");
                }
            }

            // Decrypt the API key
            var decryptedKey = _encryptionService.Decrypt(config.GeminiApiKey);

            // Use consistent path logic with FileProcessingService
            var projectDir = Path.Combine(_uploadBasePath, "knowledge", projectId.ToString());
            var filePath = Path.Combine(projectDir, doc.StoredFileName ?? "");
            
            if (string.IsNullOrEmpty(doc.StoredFileName) || !System.IO.File.Exists(filePath))
            {
                // Fallback: try to find any file in the directory that might be this one if StoredFileName was missing (for older records)
                if (System.IO.Directory.Exists(projectDir))
                {
                    var fallbackPath = Directory.GetFiles(projectDir).FirstOrDefault(f => f.Contains(doc.Id.ToString("N")));
                    if (!string.IsNullOrEmpty(fallbackPath)) filePath = fallbackPath;
                }
            }

            if (!System.IO.File.Exists(filePath))
            {
                return BadRequest("Original file not found for retry. Please delete and re-upload.");
            }

            using var stream = System.IO.File.OpenRead(filePath);
            await _fileService.ProcessKnowledgeDocumentAsync(projectId, stream, doc.FileName, doc.ContentType, decryptedKey);

            return Ok(new { Message = "Retry started", Status = doc.Status.ToString() });
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
