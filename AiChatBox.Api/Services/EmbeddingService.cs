using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;
using AiChatBox.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace AiChatBox.Api.Services
{
    public class EmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<EmbeddingService> logger, IAiLoggingService aiLogger, IServiceProvider serviceProvider) : IEmbeddingService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly string _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        private readonly ILogger<EmbeddingService> _logger = logger;
        private readonly IAiLoggingService _aiLogger = aiLogger;
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task<Vector> GetEmbeddingAsync(string text, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null)
        {
            var apiKey = apiKeyOverride;

            if (string.IsNullOrEmpty(apiKey) && projectId.HasValue)
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
                var encryptionService = scope.ServiceProvider.GetRequiredService<EncryptionService>();
                
                var pConfig = await db.Configurations
                    .FirstOrDefaultAsync(c => c.ProjectId == projectId.Value);
                
                if (pConfig != null && !string.IsNullOrEmpty(pConfig.GeminiApiKey))
                {
                    apiKey = encryptionService.Decrypt(pConfig.GeminiApiKey);
                }
            }

            if (string.IsNullOrEmpty(apiKey)) apiKey = _apiKey;

            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Gemini API key is not configured for embeddings.");

            var startTime = DateTime.UtcNow;
            string? errorMessage = null;

            var requestBody = new
            {
                model = "models/gemini-embedding-2",
                content = new
                {
                    parts = new[] { new { text } }
                },
                outputDimensionality = 3072
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent?key={apiKey}";
            
            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    errorMessage = error;
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
                errorMessage ??= ex.Message;
                _logger.LogError(ex, "Failed to generate embedding");
                throw;
            }
            finally
            {
                await _aiLogger.LogRequestAsync(new AiRequestLog
                {
                    ProjectId = projectId,
                    UserId = userId,
                    Provider = "gemini",
                    Model = "gemini-embedding-2",
                    Endpoint = "embedContent",
                    InputTokens = text.Length / 4, // Rough estimate
                    DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = errorMessage
                });
            }
        }
    }
}
