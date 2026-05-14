using AiChatBox.Api.Data;
using AiChatBox.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AiChatBox.Api.Services
{
    public class FirecrawlBackgroundService(
        IDbContextFactory<ChatDbContext> dbFactory,
        IServiceScopeFactory scopeFactory,
        EncryptionService encryption,
        FirecrawlService firecrawl,
        IConfiguration configuration,
        ILogger<FirecrawlBackgroundService> logger)
    {
        private readonly IDbContextFactory<ChatDbContext> _dbFactory = dbFactory;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly EncryptionService _encryption = encryption;
        private readonly FirecrawlService _firecrawl = firecrawl;
        private readonly IConfiguration _configuration = configuration;
        private readonly ILogger<FirecrawlBackgroundService> _logger = logger;

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("FirecrawlBackgroundService Job is starting.");

            try
            {
                await ProcessJobsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing firecrawl jobs.");
            }
        }

        private async Task ProcessJobsAsync(CancellationToken stoppingToken)
        {
            using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
            
            // 1. Start pending jobs
            var pendingJobs = await db.WebsiteCrawlJobs
                .Include(j => j.Project)
                .ThenInclude(p => p!.Configurations)
                .Where(j => j.Status == KnowledgeDocumentStatus.Pending)
                .ToListAsync(stoppingToken);

            foreach (var job in pendingJobs)
            {
                await StartFirecrawlJobAsync(db, job, stoppingToken);
            }

            // 2. Poll processing jobs
            var processingJobs = await db.WebsiteCrawlJobs
                .Include(j => j.Project)
                .ThenInclude(p => p!.Configurations)
                .Where(j => j.Status == KnowledgeDocumentStatus.Processing && !string.IsNullOrEmpty(j.FirecrawlJobId))
                .ToListAsync(stoppingToken);

            foreach (var job in processingJobs)
            {
                await PollFirecrawlJobAsync(db, job, stoppingToken);
            }
        }

        private async Task StartFirecrawlJobAsync(ChatDbContext db, WebsiteCrawlJob job, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("Starting Firecrawl job for {Url}", job.BaseUrl);
                
                var config = job.Project?.Configurations.FirstOrDefault();
                string? customKey = null;
                if (config?.FirecrawlApiKey != null)
                {
                    customKey = _encryption.Decrypt(config.FirecrawlApiKey);
                }

                var publicUrl = _configuration["Network:PublicApiUrl"];
                var webhookUrl = !string.IsNullOrEmpty(publicUrl) ? $"{publicUrl.TrimEnd('/')}/api/firecrawl/webhook" : null;

                var firecrawlJobId = await _firecrawl.StartCrawlAsync(job.BaseUrl, job.MaxPages, customKey, webhookUrl);
                
                job.FirecrawlJobId = firecrawlJobId;
                job.Status = KnowledgeDocumentStatus.Processing;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Firecrawl job for {Url}", job.BaseUrl);
                job.Status = KnowledgeDocumentStatus.Failed;
                job.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }

        private async Task PollFirecrawlJobAsync(ChatDbContext db, WebsiteCrawlJob job, CancellationToken ct)
        {
            try
            {
                var config = job.Project?.Configurations.FirstOrDefault();
                string? customKey = null;
                if (config?.FirecrawlApiKey != null)
                {
                    customKey = _encryption.Decrypt(config.FirecrawlApiKey);
                }

                var status = await _firecrawl.GetCrawlStatusAsync(job.FirecrawlJobId!, customKey);
                
                if (status.Status == "completed")
                {
                    await ProcessResultsAsync(db, job, status, ct);
                }
                else if (status.Status == "failed")
                {
                    job.Status = KnowledgeDocumentStatus.Failed;
                    job.ErrorMessage = "Firecrawl reported job failure.";
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    // Update progress if available
                    if (status.Completed > job.PagesCrawled)
                    {
                        job.PagesCrawled = status.Completed;
                        await db.SaveChangesAsync(ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Firecrawl job {JobId}", job.FirecrawlJobId);
                // We don't mark as failed immediately on poll error, might be transient
            }
        }

        private async Task ProcessResultsAsync(ChatDbContext db, WebsiteCrawlJob job, FirecrawlStatusResponse results, CancellationToken ct)
        {
            _logger.LogInformation("Processing results for Firecrawl job {JobId}", job.FirecrawlJobId);
            
            try
            {
                var config = job.Project?.Configurations.FirstOrDefault(c => !string.IsNullOrEmpty(c.GeminiApiKey));
                if (config == null) throw new Exception("No project configuration with a Gemini API key found for embeddings.");
                
                var geminiKey = _encryption.Decrypt(config.GeminiApiKey);

                if (results.Data != null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var fileService = scope.ServiceProvider.GetRequiredService<FileProcessingService>();

                    int count = 0;
                    foreach (var page in results.Data)
                    {
                        var fileName = _firecrawl.GenerateFileName(page);
                        if (!await db.KnowledgeDocuments.AnyAsync(d => d.ProjectId == job.ProjectId && d.FileName == fileName, ct))
                        {
                            await _firecrawl.ProcessPageAsync(job.ProjectId, page, geminiKey, fileService, fileName);
                        }
                        count++;
                    }
                    
                    job.PagesCrawled = count;
                }

                job.Status = KnowledgeDocumentStatus.Completed;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process results for job {JobId}", job.FirecrawlJobId);
                job.Status = KnowledgeDocumentStatus.Failed;
                job.ErrorMessage = ex.Message;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
