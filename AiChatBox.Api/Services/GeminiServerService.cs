using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AiChatBox.Api.Services
{
    public class GeminiServerService(HttpClient httpClient, IConfiguration configuration, FileProcessingService fileService, ILogger<GeminiServerService> logger) : ILlmProviderService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly string _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        private readonly string _defaultModel = configuration["Gemini:DefaultModel"] ?? "gemini-3.1-flash-lite-preview";
        private readonly FileProcessingService _fileService = fileService;
        private readonly ILogger<GeminiServerService> _logger = logger;

        public async IAsyncEnumerable<LlmResponseChunk> StreamGenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            IEnumerable<ITool>? tools = null,
            string? modelName = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Gemini API key is not configured.");

            var toolDeclarations = tools?.Select(t => new
            {
                function_declarations = new[]
                {
                    new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.ParametersSchema
                    }
                }
            }).ToArray();

            var requestBody = await BuildRequestBodyAsync(messages, systemPrompt, toolDeclarations);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var modelToUse = string.IsNullOrEmpty(modelName) ? _defaultModel : modelName;
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:streamGenerateContent?key={_apiKey}&alt=sse")
            {
                Content = jsonContent
            };
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            HttpResponseMessage response;
            try
            {
                // Create a linked token with a timeout to avoid hanging
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(2));

                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("Gemini API request timed out after 2 minutes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reach Gemini API");
                throw new InvalidOperationException($"Failed to reach Gemini API: {ex.Message}", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini API returned status code {response.StatusCode}: {errorContent}");
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

                    JsonElement json;
                    try
                    {
                        json = JsonSerializer.Deserialize<JsonElement>(data);
                    }
                    catch
                    {
                        continue;
                    }

                    var chunk = ExtractChunkResponse(json);
                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }
            }
        }

        private LlmResponseChunk? ExtractChunkResponse(JsonElement json)
        {
            try
            {
                if (!json.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    return null;

                var candidate = candidates[0];
                if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                    return null;

                var part = parts[0];
                if (part.TryGetProperty("text", out var text))
                {
                    return new LlmResponseChunk { Text = text.GetString() };
                }

                if (part.TryGetProperty("functionCall", out var functionCall))
                {
                    return new LlmResponseChunk
                    {
                        ToolCall = new ToolCall
                        {
                            Name = functionCall.GetProperty("name").GetString() ?? "",
                            ArgumentsJson = functionCall.GetProperty("args").GetRawText()
                        }
                    };
                }

                return null;
            }
            catch { return null; }
        }

        public async Task<string> GenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            object[]? toolDeclarations = null,
            string? modelName = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("Gemini API key is not configured.");

            var requestBody = await BuildRequestBodyAsync(messages, systemPrompt, toolDeclarations);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var modelToUse = string.IsNullOrEmpty(modelName) ? _defaultModel : modelName;
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{modelToUse}:generateContent?key={_apiKey}")
            {
                Content = jsonContent
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API returned {StatusCode}: {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini API returned status code {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseJson);
            return ExtractTextResponse(parsed);
        }

        private async Task<object> BuildRequestBodyAsync(IEnumerable<GenericChatMessage> messages, string? systemPrompt = null, object[]? toolDeclarations = null)
        {
            var contents = new List<object>();

            foreach (var msg in messages)
            {
                if (msg.Role == "function")
                {
                    try
                    {
                        var parsed = JsonDocument.Parse(msg.Content).RootElement;
                        var toolName = parsed.GetProperty("toolName").GetString();
                        var result = parsed.GetProperty("result");
                        
                        contents.Add(new 
                        { 
                            role = "function", 
                            parts = new[] 
                            { 
                                new { functionResponse = new { name = toolName, response = result } } 
                            } 
                        });
                        continue;
                    }
                    catch
                    {
                        // Fallback if parsing fails
                        contents.Add(new { role = "user", parts = new[] { new { text = msg.Content } } });
                        continue;
                    }
                }
                
                var role = msg.Role == "user" ? "user" : "model";
                
                if (role == "model" && !string.IsNullOrEmpty(msg.Content) && msg.Content.TrimStart().StartsWith("{"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(msg.Content);
                        if (doc.RootElement.TryGetProperty("toolCall", out var toolCall))
                        {
                            var name = toolCall.GetProperty("name").GetString();
                            var args = toolCall.GetProperty("args").GetRawText();
                            
                            contents.Add(new
                            {
                                role = "model",
                                parts = new[]
                                {
                                    new { functionCall = new { name, args = JsonDocument.Parse(args).RootElement } }
                                }
                            });
                            continue;
                        }
                    }
                    catch { /* Fallback to text */ }
                }

                var parts = await BuildMessagePartsAsync(msg.Content, msg.ImageDataUrl, msg.AttachedFileId);
                
                if (parts.Length == 0) 
                {
                    parts = new[] { new { text = "[Empty Message]" } };
                }
                
                contents.Add(new { role, parts });
            }

            if (contents.Count == 0)
            {
                contents.Add(new { role = "user", parts = new[] { new { text = "Hello" } } });
            }

            var body = new Dictionary<string, object>
            {
                ["contents"] = contents,
                ["generationConfig"] = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 2048
                }
            };

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                body["systemInstruction"] = new
                {
                    parts = new[] { new { text = systemPrompt } }
                };
            }

            if (toolDeclarations != null && toolDeclarations.Length > 0)
            {
                body["tools"] = toolDeclarations;
            }

            return body;
        }

        private async Task<object[]> BuildMessagePartsAsync(string text, string? imageDataUrl, Guid? attachedFileId)
        {
            var parts = new List<object>();

            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(new { text });
            }

            if (!string.IsNullOrEmpty(imageDataUrl))
            {
                var imagePart = ConvertDataUrlToGeminiImage(imageDataUrl);
                if (imagePart != null)
                {
                    parts.Add(imagePart);
                }
            }

            if (attachedFileId.HasValue)
            {
                var file = await _fileService.GetFileAsync(attachedFileId.Value);
                if (file != null && file.ContentType.StartsWith("image/"))
                {
                    var uploadBasePath = configuration["FileStorage:BasePath"] ?? Path.Combine("wwwroot", "uploads", "chat");
                    var filePath = Path.Combine(uploadBasePath, file.UserId, file.StoredFileName);
                    if (File.Exists(filePath))
                    {
                        var bytes = await File.ReadAllBytesAsync(filePath);
                        parts.Add(new
                        {
                            inlineData = new
                            {
                                mimeType = file.ContentType,
                                data = Convert.ToBase64String(bytes)
                            }
                        });
                    }
                }
            }

            return [.. parts];
        }

        private object? ConvertDataUrlToGeminiImage(string dataUrl)
        {
            var parts = dataUrl.Split(',');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid data URL format: {DataUrlPrefix}...", dataUrl.Length > 20 ? dataUrl[..20] : dataUrl);
                return null;
            }

            try
            {
                var mimeType = parts[0].Replace("data:", "").Replace(";base64", "");
                var base64Data = parts[1];

                return new
                {
                    inlineData = new
                    {
                        mimeType,
                        data = base64Data
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse data URL");
                return null;
            }
        }

        private string ExtractTextResponse(JsonElement responseJson)
        {
            try
            {
                if (!responseJson.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                    return string.Empty;

                var firstCandidate = candidates[0];

                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var contentParts) &&
                    contentParts.GetArrayLength() > 0)
                {
                    if (contentParts[0].TryGetProperty("text", out var textElement))
                    {
                        return textElement.GetString() ?? string.Empty;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to extract text from Gemini response: {Message}", ex.Message);
                return string.Empty;
            }
        }
        
        public int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
        
        public static int StaticEstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
