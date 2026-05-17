using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Interfaces;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// A generic LLM provider service that works with any OpenAI-compatible chat completions API.
    /// Configure with a base URL and API key to target different providers:
    /// OpenAI, Groq, Together AI, Fireworks, Mistral, OpenRouter, DeepInfra, Cerebras, SambaNova, etc.
    /// </summary>
    public class OpenAiCompatibleService : ILlmProviderService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _defaultApiKey;
        private readonly string _defaultModel;
        private readonly string _providerName;
        private readonly ILogger<OpenAiCompatibleService> _logger;

        public OpenAiCompatibleService(
            HttpClient httpClient,
            string baseUrl,
            string defaultApiKey,
            string defaultModel,
            string providerName,
            ILogger<OpenAiCompatibleService> logger)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl.TrimEnd('/');
            _defaultApiKey = defaultApiKey;
            _defaultModel = defaultModel;
            _providerName = providerName;
            _logger = logger;
        }

        private string CompletionsUrl => $"{_baseUrl}/chat/completions";

        public async IAsyncEnumerable<LlmResponseChunk> StreamGenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            IEnumerable<ITool>? tools = null,
            string? modelName = null,
            string? apiKeyOverride = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var apiKey = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _defaultApiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException($"{_providerName} API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, tools, modelName, stream: true);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

            HttpResponseMessage response;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(2));
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException($"{_providerName} API request timed out after 2 minutes.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reach {Provider} API at {BaseUrl}", _providerName, _baseUrl);
                throw new InvalidOperationException($"Failed to reach {_providerName} API: {ex.Message}", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("{Provider} API returned {StatusCode}: {Error}", _providerName, response.StatusCode, errorContent);
                throw new HttpRequestException($"{_providerName} API returned status code {response.StatusCode}: {errorContent}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var toolCallAccumulator = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

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

                    if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("delta", out var delta))
                        {
                            if (delta.TryGetProperty("tool_calls", out var toolCalls))
                            {
                                foreach (var tc in toolCalls.EnumerateArray())
                                {
                                    var index = tc.GetProperty("index").GetInt32();
                                    if (!toolCallAccumulator.ContainsKey(index))
                                        toolCallAccumulator[index] = ("", "", new StringBuilder());

                                    var (id, name, args) = toolCallAccumulator[index];

                                    if (tc.TryGetProperty("id", out var idEl))
                                        id = idEl.GetString() ?? "";
                                    if (tc.TryGetProperty("function", out var func) &&
                                        func.TryGetProperty("name", out var nameEl))
                                        name = nameEl.GetString() ?? "";
                                    if (tc.TryGetProperty("function", out var func2) &&
                                        func2.TryGetProperty("arguments", out var argsEl))
                                        args.Append(argsEl.GetString() ?? "");

                                    toolCallAccumulator[index] = (id, name, args);
                                }
                            }

                            if (delta.TryGetProperty("content", out var content))
                            {
                                var text = content.GetString();
                                if (!string.IsNullOrEmpty(text))
                                    yield return new LlmResponseChunk { Text = text };
                            }
                        }
                    }
                }
            }

            foreach (var (_, (id, name, args)) in toolCallAccumulator.OrderBy(kvp => kvp.Key))
            {
                yield return new LlmResponseChunk
                {
                    ToolCalls = new List<ToolCall> { new ToolCall { Id = id, Name = name, ArgumentsJson = args.ToString() } }
                };
            }
        }

        public async Task<string> GenerateContentAsync(
            IEnumerable<GenericChatMessage> messages,
            string? systemPrompt = null,
            object[]? toolDeclarations = null,
            string? modelName = null,
            string? apiKeyOverride = null,
            CancellationToken cancellationToken = default)
        {
            var apiKey = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _defaultApiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException($"{_providerName} API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, null, modelName, stream: false);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, CompletionsUrl)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("{Provider} API returned {StatusCode}: {Error}", _providerName, response.StatusCode, errorContent);
                throw new HttpRequestException($"{_providerName} API returned status code {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<JsonElement>(responseJson);
            return ExtractTextResponse(parsed);
        }

        private object BuildRequestBody(IEnumerable<GenericChatMessage> messages, string? systemPrompt, IEnumerable<ITool>? tools, string? modelName, bool stream)
        {
            var openAiMessages = new List<object>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                openAiMessages.Add(new { role = "system", content = systemPrompt });
            }

            foreach (var msg in messages)
            {
                if (msg.Role == "user")
                {
                    openAiMessages.Add(new { role = "user", content = msg.Content });
                }
                else if (msg.Role == "model")
                {
                    if (!string.IsNullOrEmpty(msg.Content) && msg.Content.TrimStart().StartsWith("{"))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(msg.Content);
                            var root = doc.RootElement;
                            
                            List<object> openAiToolCalls = new();
                            if (root.TryGetProperty("toolCalls", out var tcs))
                            {
                                foreach (var tc in tcs.EnumerateArray())
                                {
                                    var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString();
                                    var name = tc.GetProperty("name").GetString();
                                    var args = tc.GetProperty("args").GetRawText();
                                    openAiToolCalls.Add(new { id, type = "function", function = new { name, arguments = args } });
                                }
                            }
                            else if (root.TryGetProperty("toolCall", out var tc))
                            {
                                var id = tc.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString();
                                var name = tc.GetProperty("name").GetString();
                                var args = tc.GetProperty("args").GetRawText();
                                openAiToolCalls.Add(new { id, type = "function", function = new { name, arguments = args } });
                            }

                            if (openAiToolCalls.Count > 0)
                            {
                                openAiMessages.Add(new 
                                { 
                                    role = "assistant", 
                                    tool_calls = openAiToolCalls.ToArray()
                                });
                                continue;
                            }
                        }
                        catch { }
                    }
                    openAiMessages.Add(new { role = "assistant", content = msg.Content });
                }
                else if (msg.Role == "function")
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(msg.Content);
                        var toolCallId = doc.RootElement.TryGetProperty("toolCallId", out var idEl) ? idEl.GetString() : "";
                        var result = doc.RootElement.GetProperty("result").GetRawText();
                        
                        openAiMessages.Add(new 
                        { 
                            role = "tool", 
                            tool_call_id = toolCallId, 
                            content = result 
                        });
                    }
                    catch 
                    {
                        openAiMessages.Add(new { role = "assistant", content = msg.Content });
                    }
                }
            }

            var body = new Dictionary<string, object>
            {
                ["model"] = string.IsNullOrEmpty(modelName) ? _defaultModel : modelName,
                ["messages"] = openAiMessages,
                ["stream"] = stream,
                ["temperature"] = 0.1,
                ["max_tokens"] = 2048
            };

            if (tools != null)
            {
                var toolsList = tools.ToList();
                if (toolsList.Count > 0)
                {
                    var openAiTools = toolsList.Select(t => new
                    {
                        type = "function",
                        function = new
                        {
                            name = SanitizeToolName(t.Name),
                            description = t.Description,
                            parameters = (object?)t.ParametersSchema ?? new { type = "object", properties = new { } }
                        }
                    }).ToList<object>();

                    body["tools"] = openAiTools;
                    body["tool_choice"] = "auto";
                }
            }

            return body;
        }

        private static string SanitizeToolName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed_tool";
            var sanitized = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_-]", "_");
            if (sanitized.Length > 64) sanitized = sanitized[..64];
            return sanitized;
        }

        private static string ExtractTextResponse(JsonElement json)
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
