using System.Text.Json;
using AiChatBox.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/provider")]
    public class ModelsController(HttpClient httpClient) : ControllerBase
    {
        private readonly HttpClient _http = httpClient;

        [HttpGet("models")]
        public async Task<ActionResult<List<ProviderModel>>> GetModels([FromQuery] string provider, [FromQuery] string apiKey)
        {
            if (string.IsNullOrEmpty(provider) || string.IsNullOrEmpty(apiKey))
                return BadRequest("Provider and apiKey are required.");

            return provider.ToLowerInvariant() switch
            {
                "gemini" => await FetchGeminiModels(apiKey),
                "groq" => await FetchGroqModels(apiKey),
                "openai" => await FetchOpenAiModels(apiKey),
                _ => BadRequest($"Unknown provider: {provider}")
            };
        }

        private async Task<ActionResult<List<ProviderModel>>> FetchGeminiModels(string apiKey)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return BadRequest(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var models = new List<ProviderModel>();

                if (json.TryGetProperty("models", out var arr))
                {
                    foreach (var m in arr.EnumerateArray())
                    {
                        var name = m.GetProperty("name").GetString() ?? "";
                        var nameParts = name.Split('/');
                        var shortName = nameParts.Length > 1 ? nameParts[^1] : name;

                        var methods = new List<string>();
                        if (m.TryGetProperty("supportedGenerationMethods", out var methodsArr))
                            foreach (var mm in methodsArr.EnumerateArray())
                                methods.Add(mm.GetString() ?? "");

                        var desc = m.TryGetProperty("description", out var d) ? d.GetString() : "";

                        if (methods.Contains("generateContent"))
                        {
                            models.Add(new ProviderModel { Id = shortName, Name = m.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? shortName : shortName, Description = desc ?? "" });
                        }
                    }
                }

                return models.OrderBy(m => m.Name).ToList();
            }
            catch
            {
                return GetGeminiFallbackModels();
            }
        }

        private async Task<ActionResult<List<ProviderModel>>> FetchGroqModels(string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return BadRequest(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var models = new List<ProviderModel>();

                if (json.TryGetProperty("data", out var arr))
                {
                    foreach (var m in arr.EnumerateArray())
                    {
                        models.Add(new ProviderModel
                        {
                            Id = m.GetProperty("id").GetString() ?? "",
                            Name = m.GetProperty("id").GetString() ?? "",
                            Description = m.TryGetProperty("owned_by", out var owned) ? owned.GetString() ?? "" : ""
                        });
                    }
                }

                return models.OrderBy(m => m.Id).ToList();
            }
            catch
            {
                return GetGroqFallbackModels();
            }
        }

        private async Task<ActionResult<List<ProviderModel>>> FetchOpenAiModels(string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return BadRequest(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var models = new List<ProviderModel>();

                if (json.TryGetProperty("data", out var arr))
                {
                    foreach (var m in arr.EnumerateArray())
                    {
                        models.Add(new ProviderModel
                        {
                            Id = m.GetProperty("id").GetString() ?? "",
                            Name = m.GetProperty("id").GetString() ?? "",
                            Description = m.TryGetProperty("owned_by", out var owned) ? owned.GetString() ?? "" : ""
                        });
                    }
                }

                return models.Where(m => m.Id.StartsWith("gpt") || m.Id.StartsWith("o1") || m.Id.StartsWith("o3")).OrderBy(m => m.Id).ToList();
            }
            catch
            {
                return GetOpenAiFallbackModels();
            }
        }

        private static List<ProviderModel> GetGeminiFallbackModels() =>
        [
            new() { Id = "gemini-1.5-flash", Name = "Gemini 1.5 Flash", Description = "Fast and efficient for most tasks" },
            new() { Id = "gemini-1.5-pro", Name = "Gemini 1.5 Pro", Description = "Advanced reasoning capabilities" },
            new() { Id = "gemini-2.0-flash", Name = "Gemini 2.0 Flash", Description = "Next-gen speed and quality" },
            new() { Id = "gemini-2.5-flash-preview-06-17", Name = "Gemini 2.5 Flash", Description = "Latest preview with enhanced reasoning" }
        ];

        private static List<ProviderModel> GetGroqFallbackModels() =>
        [
            new() { Id = "llama-3.3-70b-versatile", Name = "Llama 3.3 70B Versatile", Description = "Best all-around" },
            new() { Id = "llama-3.1-8b-instant", Name = "Llama 3.1 8B Instant", Description = "Fast and lightweight" },
            new() { Id = "mixtral-8x7b-32768", Name = "Mixtral 8x7B", Description = "Strong reasoning" },
            new() { Id = "deepseek-r1-distill-llama-70b", Name = "DeepSeek R1 70B", Description = "Advanced reasoning model" }
        ];

        private static List<ProviderModel> GetOpenAiFallbackModels() =>
        [
            new() { Id = "gpt-4o", Name = "GPT-4o", Description = "Most capable multimodal model" },
            new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini", Description = "Affordable and efficient" },
            new() { Id = "gpt-4-turbo", Name = "GPT-4 Turbo", Description = "Powerful reasoning" },
            new() { Id = "o3-mini", Name = "O3 Mini", Description = "Advanced reasoning, compact" }
        ];
    }
}
