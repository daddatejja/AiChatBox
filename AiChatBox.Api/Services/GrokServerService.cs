using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiChatBox.Api.Services
{
    public class GrokServerService(HttpClient httpClient, IConfiguration configuration, ILogger<GrokServerService> logger) : ILlmProviderService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly string _apiKey = configuration["Grok:ApiKey"] ?? string.Empty;
        private readonly string _modelEndpoint = "https://api.groq.com/openai/v1/chat/completions";
        private readonly string _defaultModel = "llama-3.3-70b-versatile";
        private readonly ILogger<GrokServerService> _logger = logger;

        public async IAsyncEnumerable<LlmResponseChunk> StreamGenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            IEnumerable<ITool>? tools = null,
            string? modelName = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Grok API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, modelName, stream: true);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _modelEndpoint)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reach Grok API");
                throw new InvalidOperationException($"Failed to reach Grok API: {ex.Message}", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Grok API returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Grok API returned status code {response.StatusCode}: {errorContent}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var data = line["data: ".Length..].Trim();
                    if (data == "[DONE]") break;

                    JsonElement json;
                    try
                    {
                        json = JsonSerializer.Deserialize<JsonElement>(data);
                    }
                    catch
                    {
                        continue;
                    }

                    var textChunk = ExtractTextFromStreamChunk(json);
                    if (!string.IsNullOrEmpty(textChunk))
                    {
                        yield return new LlmResponseChunk { Text = textChunk };
                    }
                }
            }
        }

        public async Task<string> GenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            object[]? toolDeclarations = null,
            string? modelName = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Grok API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, modelName, stream: false);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _modelEndpoint)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Grok API returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Grok API returned status code {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseJson);
            return ExtractTextResponse(parsed);
        }

        private object BuildRequestBody(IEnumerable<GenericChatMessage> messages, string? systemPrompt = null, string? modelName = null, bool stream = false)
        {
            var openAiMessages = new List<object>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                openAiMessages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var msg in messages)
            {
                var role = msg.Role == "user" ? "user" : "assistant";
                openAiMessages.Add(new { role, content = msg.Content });
            }

            return new
            {
                model = string.IsNullOrEmpty(modelName) ? _defaultModel : modelName,
                messages = openAiMessages,
                stream = stream,
                temperature = 0.1,
                max_tokens = 2048
            };
        }

        private string ExtractTextFromStreamChunk(JsonElement json)
        {
            if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private string ExtractTextResponse(JsonElement json)
        {
            if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        public int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
