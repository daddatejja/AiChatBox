using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [ApiController]
    [Route("api/firecrawl/webhook")]
    [AllowAnonymous]
    public class FirecrawlWebhookController(
        ChatDbContext db,
        FirecrawlService firecrawl,
        FileProcessingService fileService,
        EncryptionService encryption,
        ILogger<FirecrawlWebhookController> logger) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly FirecrawlService _firecrawl = firecrawl;
        private readonly FileProcessingService _fileService = fileService;
        private readonly EncryptionService _encryption = encryption;
        private readonly ILogger<FirecrawlWebhookController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] FirecrawlWebhookPayload payload)
        {
            if (string.IsNullOrEmpty(payload.Id)) return BadRequest("Missing Job ID.");

            var job = await _db.WebsiteCrawlJobs
                .Include(j => j.Project)
                .ThenInclude(p => p!.Configurations)
                .FirstOrDefaultAsync(j => j.FirecrawlJobId == payload.Id);

            if (job == null)
            {
                _logger.LogWarning("Firecrawl webhook received for unknown job: {JobId}", payload.Id);
                // Return Ok anyway so Firecrawl doesn't keep retrying unknown jobs
                return Ok();
            }

            _logger.LogInformation("Firecrawl webhook {Type} received for job {JobId}", payload.Type, payload.Id);

            try
            {
                switch (payload.Type)
                {
                    case "page":
                    case "crawl.page":
                        if (payload.Data != null)
                        {
                            var config = job.Project?.Configurations.FirstOrDefault(c => !string.IsNullOrEmpty(c.GeminiApiKey));
                            if (config != null)
                            {
                                var geminiKey = _encryption.Decrypt(config.GeminiApiKey);
                                foreach (var page in payload.Data)
                                {
                                    var fileName = _firecrawl.GenerateFileName(page);
                                    if (!await _db.KnowledgeDocuments.AnyAsync(d => d.ProjectId == job.ProjectId && d.FileName == fileName))
                                    {
                                        await _firecrawl.ProcessPageAsync(job.ProjectId, page, geminiKey, _fileService, fileName);
                                        job.PagesCrawled++;
                                    }
                                }
                                await _db.SaveChangesAsync();
                            }
                        }
                        break;

                    case "completed":
                    case "crawl.completed":
                        job.Status = KnowledgeDocumentStatus.Completed;
                        await _db.SaveChangesAsync();
                        break;

                    case "failed":
                    case "crawl.failed":
                        job.Status = KnowledgeDocumentStatus.Failed;
                        job.ErrorMessage = payload.Error ?? "Firecrawl reported failure.";
                        await _db.SaveChangesAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Firecrawl webhook for job {JobId}", payload.Id);
                return StatusCode(500);
            }

            return Ok();
        }
    }
}
