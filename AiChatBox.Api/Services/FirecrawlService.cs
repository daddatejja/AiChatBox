using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AiChatBox.Api.Services
{
    public class FirecrawlService(HttpClient httpClient, IConfiguration configuration, ILogger<FirecrawlService> logger)
    {
        private readonly HttpClient _http = httpClient;
        private readonly ILogger<FirecrawlService> _logger = logger;
        private readonly string _defaultApiKey = configuration["Firecrawl:ApiKey"] ?? "";
        private readonly string _apiUrl = configuration["Firecrawl:ApiUrl"] ?? "https://api.firecrawl.dev/v1";

        public async Task<string> StartCrawlAsync(string url, int maxPages, string? customApiKey = null, string? webhookUrl = null)
        {
            var apiKey = !string.IsNullOrEmpty(customApiKey) ? customApiKey : _defaultApiKey;
            if (string.IsNullOrEmpty(apiKey)) throw new Exception("Firecrawl API Key is not configured.");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var requestBody = new
            {
                url = url,
                limit = maxPages,
                scrapeOptions = new
                {
                    formats = new[] { "markdown" }
                },
                webhook = !string.IsNullOrEmpty(webhookUrl) ? new
                {
                    url = webhookUrl,
                    events = new[] { "started", "page", "completed", "failed" }
                } : null
            };

            var response = await _http.PostAsync($"{_apiUrl}/crawl", 
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Firecrawl StartCrawl failed: {Error}", error);
                throw new Exception($"Firecrawl API error: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<FirecrawlCrawlResponse>();
            return result?.Id ?? throw new Exception("Failed to get Job ID from Firecrawl.");
        }

        public async Task<FirecrawlStatusResponse> GetCrawlStatusAsync(string jobId, string? customApiKey = null)
        {
            var apiKey = !string.IsNullOrEmpty(customApiKey) ? customApiKey : _defaultApiKey;
            
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _http.GetAsync($"{_apiUrl}/crawl/{jobId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("Firecrawl GetCrawlStatus failed for job {JobId}: {Error}", jobId, error);
                throw new Exception($"Firecrawl API error: {response.StatusCode}");
            }

            return await response.Content.ReadFromJsonAsync<FirecrawlStatusResponse>() 
                   ?? throw new Exception("Failed to parse Firecrawl status response.");
        }

        public bool IsTechnicalFile(FirecrawlPage page)
        {
            var url = page.Metadata?.SourceURL?.ToLower() ?? "";
            var title = page.Metadata?.Title?.ToLower() ?? "";

            // 1. Check for common technical file patterns in URL
            string[] technicalPatterns = { 
                "sitemap", ".xml", ".rss", ".atom", "robots.txt", 
                "manifest.json", ".webmanifest", "wp-json", "opensearch.xml" 
            };

            if (technicalPatterns.Any(p => url.Contains(p)))
            {
                return true;
            }

            // 2. Check for technical titles
            if (title.Contains("sitemap") || title.Contains("index of /"))
            {
                return true;
            }

            // 3. Heuristic: If it's a huge list of URLs and very little else, it's likely a sitemap
            // (Only check if we have markdown)
            if (!string.IsNullOrEmpty(page.Markdown))
            {
                var lineCount = page.Markdown.Split('\n').Length;
                var urlCount = System.Text.RegularExpressions.Regex.Matches(page.Markdown, @"https?://").Count;
                
                // If more than 50% of lines look like URLs and there are more than 10 URLs, it's likely a list
                if (urlCount > 10 && (double)urlCount / lineCount > 0.5)
                {
                    return true;
                }
            }

            return false;
        }

        public string GenerateFileName(FirecrawlPage page)
        {
            var url = page.Metadata?.SourceURL ?? $"page_{Guid.NewGuid():N}";
            var fileName = url.Replace("https://", "").Replace("http://", "").Replace("/", "_");
            // Remove potential query params or characters that are invalid in filenames
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            if (!fileName.EndsWith(".md")) fileName += ".md";
            return fileName;
        }

        public async Task ProcessPageAsync(Guid projectId, FirecrawlPage page, string geminiApiKey, FileProcessingService fileService, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(page.Markdown)) return;

            // Skip technical files (sitemaps, RSS, etc)
            if (IsTechnicalFile(page))
            {
                _logger.LogInformation("Skipping technical file: {Url}", page.Metadata?.SourceURL);
                return;
            }

            var finalFileName = fileName ?? GenerateFileName(page);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(page.Markdown));
            await fileService.ProcessKnowledgeDocumentAsync(projectId, ms, finalFileName, "text/markdown", geminiApiKey);
        }
    }

    public class FirecrawlWebhookPayload
    {
        public string? Type { get; set; } // "crawl.started", "crawl.page", "crawl.completed", "crawl.failed"
        public string? Id { get; set; }   // Job ID
        public List<FirecrawlPage>? Data { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class FirecrawlCrawlResponse
    {
        public string? Id { get; set; }
        public bool Success { get; set; }
        public string? Url { get; set; }
    }

    public class FirecrawlStatusResponse
    {
        public string? Status { get; set; } // "scraping", "completed", "failed"
        public int Total { get; set; }
        public int Completed { get; set; }
        public int CreditsUsed { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<FirecrawlPage>? Data { get; set; }
    }

    public class FirecrawlPage
    {
        public string? Markdown { get; set; }
        public FirecrawlMetadata? Metadata { get; set; }
    }

    public class FirecrawlMetadata
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? SourceURL { get; set; }
    }
}
