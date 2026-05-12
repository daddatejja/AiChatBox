using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace AiChatBox.Api.Services
{
    public class EmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<EmbeddingService> logger)
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly string _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        private readonly ILogger<EmbeddingService> _logger = logger;

        public async Task<Vector> GetEmbeddingAsync(string text, string? apiKeyOverride = null)
        {
            var apiKey = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _apiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Gemini API key is not configured for embeddings.");

            var requestBody = new
            {
                model = "models/text-embedding-004",
                content = new
                {
                    parts = new[] { new { text } }
                }
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent?key={apiKey}";
            
            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini Embedding API error: {Error}", error);
                    throw new HttpRequestException($"Gemini Embedding API returned {response.StatusCode}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var embeddingArray = doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(v => (float)v.GetDouble())
                    .ToArray();

                return new Vector(embeddingArray);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding");
                throw;
            }
        }
    }
}
