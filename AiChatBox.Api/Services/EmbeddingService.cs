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

        // Static throttle to prevent hitting RPM limits across multiple concurrent processing tasks
        private static readonly SemaphoreSlim _globalThrottle = new SemaphoreSlim(2, 2);

        private async Task<string> ResolveApiKeyAsync(string? apiKeyOverride, Guid? projectId)
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

            return apiKey;
        }

        public async Task<Vector> GetEmbeddingAsync(string text, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null)
        {
            await _globalThrottle.WaitAsync();
            try
            {
                return await GetEmbeddingInternalAsync(text, apiKeyOverride, projectId, userId);
            }
            finally
            {
                // Small mandatory delay to stay under RPM limits for free tier
                await Task.Delay(500);
                _globalThrottle.Release();
            }
        }

        private async Task<Vector> GetEmbeddingInternalAsync(string text, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null)
        {
            var apiKey = await ResolveApiKeyAsync(apiKeyOverride, projectId);

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

            int maxRetries = 3;
            int delayMs = 3000;

            try
            {
                for (int retry = 0; retry <= maxRetries; retry++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync(url, jsonContent);

                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            var serverSuggestedDelay = GetRetryDelayFromError(error);
                            
                            if (retry == maxRetries)
                            {
                                errorMessage = error;
                                _logger.LogError("Gemini Embedding API quota exceeded after {Retries} retries: {Error}", maxRetries, error);
                                throw new HttpRequestException("Gemini API Quota Exceeded (429). Please check your plan or try again later.");
                            }

                            var waitTime = serverSuggestedDelay ?? delayMs;
                            _logger.LogWarning("Gemini Embedding API rate limited. Retrying in {Delay}ms... (Attempt {Retry}/{Max})", waitTime, retry + 1, maxRetries);
                            await Task.Delay(waitTime);
                            delayMs *= 2; 
                            continue;
                        }

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
                    catch (Exception ex) when (ex is not HttpRequestException || !ex.Message.Contains("429"))
                    {
                        errorMessage ??= ex.Message;
                        _logger.LogError(ex, "Failed to generate embedding");
                        throw;
                    }
                }

                throw new HttpRequestException("Failed to generate embedding after multiple attempts.");
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
                    InputTokens = text.Length / 4,
                    DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = errorMessage
                });
            }
        }

        public async Task<List<Vector>> GetBatchEmbeddingsAsync(List<string> texts, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null)
        {
            if (texts == null || texts.Count == 0) return new List<Vector>();

            await _globalThrottle.WaitAsync();
            try
            {
                return await GetBatchEmbeddingsInternalAsync(texts, apiKeyOverride, projectId, userId);
            }
            finally
            {
                // Longer delay for batch requests to be safe
                await Task.Delay(2000);
                _globalThrottle.Release();
            }
        }

        private async Task<List<Vector>> GetBatchEmbeddingsInternalAsync(List<string> texts, string? apiKeyOverride = null, Guid? projectId = null, string? userId = null)
        {
            var apiKey = await ResolveApiKeyAsync(apiKeyOverride, projectId);
            var startTime = DateTime.UtcNow;
            string? errorMessage = null;

            var requests = texts.Select(t => new
            {
                model = "models/gemini-embedding-2",
                content = new { parts = new[] { new { text = t } } },
                outputDimensionality = 3072
            }).ToList();

            var requestBody = new { requests };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:batchEmbedContents?key={apiKey}";

            int maxRetries = 3;
            int delayMs = 5000;

            try
            {
                for (int retry = 0; retry <= maxRetries; retry++)
                {
                    try
                    {
                        var response = await _httpClient.PostAsync(url, jsonContent);

                        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            var serverSuggestedDelay = GetRetryDelayFromError(error);

                            if (retry == maxRetries)
                            {
                                errorMessage = error;
                                _logger.LogError("Gemini Batch Embedding API quota exceeded after {Retries} retries: {Error}", maxRetries, error);
                                throw new HttpRequestException("Gemini API Quota Exceeded (429). Please check your plan or try again later.");
                            }

                            var waitTime = serverSuggestedDelay ?? delayMs;
                            _logger.LogWarning("Gemini Batch Embedding API rate limited. Retrying in {Delay}ms... (Attempt {Retry}/{Max})", waitTime, retry + 1, maxRetries);
                            await Task.Delay(waitTime);
                            delayMs *= 2;
                            continue;
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            var error = await response.Content.ReadAsStringAsync();
                            errorMessage = error;
                            _logger.LogError("Gemini Batch Embedding API error: {Error}", error);
                            throw new HttpRequestException($"Gemini Batch Embedding API returned {response.StatusCode}");
                        }

                        var responseJson = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseJson);
                        var vectors = doc.RootElement
                            .GetProperty("embeddings")
                            .EnumerateArray()
                            .Select(e => new Vector(e.GetProperty("values").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray()))
                            .ToList();

                        return vectors;
                    }
                    catch (Exception ex) when (ex is not HttpRequestException || !ex.Message.Contains("429"))
                    {
                        errorMessage ??= ex.Message;
                        _logger.LogError(ex, "Failed to generate batch embeddings");
                        throw;
                    }
                }

                throw new HttpRequestException("Failed to generate batch embeddings after multiple attempts.");
            }
            finally
            {
                await _aiLogger.LogRequestAsync(new AiRequestLog
                {
                    ProjectId = projectId,
                    UserId = userId,
                    Provider = "gemini",
                    Model = "gemini-embedding-2",
                    Endpoint = "batchEmbedContents",
                    InputTokens = texts.Sum(t => t.Length) / 4,
                    DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    ErrorMessage = errorMessage
                });
            }
        }

        private int? GetRetryDelayFromError(string errorJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(errorJson);
                if (doc.RootElement.TryGetProperty("error", out var errorObj) && 
                    errorObj.TryGetProperty("details", out var details) && 
                    details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        if (detail.TryGetProperty("@type", out var type) && type.GetString() == "type.googleapis.com/google.rpc.RetryInfo" &&
                            detail.TryGetProperty("retryDelay", out var delayStr))
                        {
                            var delay = delayStr.GetString();
                            if (!string.IsNullOrEmpty(delay) && delay.EndsWith("s"))
                            {
                                if (double.TryParse(delay.TrimEnd('s'), out var seconds))
                                {
                                    return (int)(seconds * 1000) + 500; // Add 500ms buffer
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
