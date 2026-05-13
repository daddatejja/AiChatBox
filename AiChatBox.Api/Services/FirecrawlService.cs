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

        public async Task<string> StartCrawlAsync(string url, int maxPages, string? customApiKey = null)
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
                }
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
