using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            string? apiKeyOverride = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var apiKey = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _apiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Grok API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, tools, modelName, stream: true);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _modelEndpoint)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
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
                    ToolCall = new ToolCall { Id = id, Name = name, ArgumentsJson = args.ToString() }
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
            var apiKey = !string.IsNullOrEmpty(apiKeyOverride) ? apiKeyOverride : _apiKey;
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Grok API key is not configured.");

            var requestBody = BuildRequestBody(messages, systemPrompt, null, modelName, stream: false);
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, _modelEndpoint)
            {
                Content = jsonContent
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

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
                            if (doc.RootElement.TryGetProperty("toolCall", out var toolCall))
                            {
                                var id = toolCall.TryGetProperty("id", out var idEl) ? idEl.GetString() : Guid.NewGuid().ToString();
                                var name = toolCall.GetProperty("name").GetString();
                                var args = toolCall.GetProperty("args").GetRawText();
                                
                                openAiMessages.Add(new 
                                { 
                                    role = "assistant", 
                                    tool_calls = new[] 
                                    { 
                                        new { id, type = "function", function = new { name, arguments = args } } 
                                    } 
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
                            name = t.Name,
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
