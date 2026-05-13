using System.Security.Claims;
using AiChatBox.Api.Data;
using AiChatBox.Api.DTOs;
using AiChatBox.Api.Models;
using AiChatBox.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiChatBox.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class ConfigurationController(ChatDbContext db, EncryptionService encryption) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly EncryptionService _encryption = encryption;
        private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet("project/{projectId}/configurations")]
        public async Task<ActionResult<IEnumerable<ConfigurationDto>>> GetConfigurations(Guid projectId)
        {
            var project = await _db.Projects.AnyAsync(p => p.Id == projectId && p.UserId == UserId);
            if (!project) return NotFound();

            var configs = await _db.Configurations
                .Where(c => c.ProjectId == projectId)
                .Select(c => new ConfigurationDto
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Name = c.Name,
                    SystemPrompt = c.SystemPrompt,
                    DefaultProvider = c.DefaultProvider,
                    DefaultModel = c.DefaultModel,
                    LiveVoiceEnabled = c.LiveVoiceEnabled,
                    HasGeminiKey = c.GeminiApiKey != null,
                    HasGroqKey = c.GroqApiKey != null,
                    HasOpenAiKey = c.OpenAiApiKey != null,
                    HasFirecrawlKey = c.FirecrawlApiKey != null,
                    RateLimitRequests = c.RateLimitRequests,
                    MaxSpendLimit = c.MaxSpendLimit,
                    CurrentSpend = c.CurrentSpend,
                    CreatedAt = c.CreatedAt,
                    ApiKeyCount = c.ApiKeys.Count
                })
                .ToListAsync();

            return Ok(configs);
        }

        [HttpGet("configuration/{id}")]
        public async Task<ActionResult<ConfigurationDetailDto>> GetConfiguration(Guid id)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            // Return masked keys instead of raw values — frontend only needs to know if they are set
            return Ok(new ConfigurationDetailDto
            {
                Id = config.Id,
                ProjectId = config.ProjectId,
                Name = config.Name,
                SystemPrompt = config.SystemPrompt,
                HasGeminiKey = config.GeminiApiKey != null,
                HasGroqKey = config.GroqApiKey != null,
                HasOpenAiKey = config.OpenAiApiKey != null,
                HasFirecrawlKey = config.FirecrawlApiKey != null,
                DefaultProvider = config.DefaultProvider,
                DefaultModel = config.DefaultModel,
                LiveVoiceEnabled = config.LiveVoiceEnabled,
                RateLimitRequests = config.RateLimitRequests,
                RateLimitWindowMinutes = config.RateLimitWindowMinutes,
                MaxSpendLimit = config.MaxSpendLimit,
                CurrentSpend = config.CurrentSpend,
                SuggestionsJson = config.SuggestionsJson,
                CreatedAt = config.CreatedAt,
                EnabledModels = config.EnabledModels
            });
        }

        [HttpPost("project/{projectId}/configurations")]
        public async Task<ActionResult<ConfigurationDto>> CreateConfiguration(Guid projectId, CreateConfigurationDto model)
        {
            var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == UserId);
            if (project == null) return NotFound();

            var config = new ProjectConfiguration
            {
                ProjectId = projectId,
                Name = model.Name,
                SystemPrompt = model.SystemPrompt,
                DefaultProvider = model.DefaultProvider,
                DefaultModel = model.DefaultModel
            };

            _db.Configurations.Add(config);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetConfiguration), new { id = config.Id }, new ConfigurationDto
            {
                Id = config.Id,
                ProjectId = config.ProjectId,
                Name = config.Name,
                SystemPrompt = config.SystemPrompt,
                DefaultProvider = config.DefaultProvider,
                DefaultModel = config.DefaultModel,
                LiveVoiceEnabled = config.LiveVoiceEnabled,
                CreatedAt = config.CreatedAt
            });
        }

        [HttpPut("configuration/{id}")]
        public async Task<IActionResult> UpdateConfiguration(Guid id, UpdateConfigurationDto model)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            if (model.Name != null) config.Name = model.Name;
            if (model.SystemPrompt != null) config.SystemPrompt = model.SystemPrompt;

            // Encrypt API keys before storing
            if (model.GeminiApiKey != null) config.GeminiApiKey = model.GeminiApiKey == "" ? null : _encryption.Encrypt(model.GeminiApiKey);
            if (model.GroqApiKey != null) config.GroqApiKey = model.GroqApiKey == "" ? null : _encryption.Encrypt(model.GroqApiKey);
            if (model.OpenAiApiKey != null) config.OpenAiApiKey = model.OpenAiApiKey == "" ? null : _encryption.Encrypt(model.OpenAiApiKey);
            if (model.FirecrawlApiKey != null) config.FirecrawlApiKey = model.FirecrawlApiKey == "" ? null : _encryption.Encrypt(model.FirecrawlApiKey);

            if (model.DefaultProvider != null) config.DefaultProvider = model.DefaultProvider;
            if (model.DefaultModel != null) config.DefaultModel = model.DefaultModel;
            if (model.LiveVoiceEnabled.HasValue) config.LiveVoiceEnabled = model.LiveVoiceEnabled.Value;
            if (model.EnabledModels != null) config.EnabledModels = model.EnabledModels;

            if (model.RateLimitRequests.HasValue) config.RateLimitRequests = model.RateLimitRequests.Value;
            if (model.RateLimitWindowMinutes.HasValue) config.RateLimitWindowMinutes = model.RateLimitWindowMinutes.Value;
            if (model.MaxSpendLimit.HasValue) config.MaxSpendLimit = model.MaxSpendLimit.Value;
            if (model.SuggestionsJson != null) config.SuggestionsJson = model.SuggestionsJson;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// Fetches available models for a provider using the configuration's stored (encrypted) API key.
        /// No API key is transmitted from the frontend.
        /// </summary>
        [HttpGet("configuration/{id}/models/{provider}")]
        public async Task<ActionResult> GetModelsForProvider(Guid id, string provider)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            // Resolve and decrypt the appropriate key
            var encryptedKey = provider.ToLowerInvariant() switch
            {
                "gemini" => config.GeminiApiKey,
                "groq" => config.GroqApiKey,
                "openai" => config.OpenAiApiKey,
                _ => null
            };

            if (string.IsNullOrEmpty(encryptedKey))
                return BadRequest($"No API key configured for provider '{provider}'.");

            var apiKey = _encryption.Decrypt(encryptedKey);

            // Delegate to the existing models controller logic
            // We use HttpContext.RequestServices to call the provider APIs
            var httpClient = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
            return provider.ToLowerInvariant() switch
            {
                "gemini" => await FetchGeminiModels(httpClient, apiKey!),
                "groq" => await FetchGroqModels(httpClient, apiKey!),
                "openai" => await FetchOpenAiModels(httpClient, apiKey!),
                _ => BadRequest($"Unknown provider: {provider}")
            };
        }

        [HttpDelete("configuration/{id}")]
        public async Task<IActionResult> DeleteConfiguration(Guid id)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            _db.Configurations.Remove(config);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // --- Private model fetch helpers (moved from ModelsController) ---

        private static async Task<ActionResult> FetchGeminiModels(HttpClient http, string apiKey)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new BadRequestObjectResult(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
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
                            models.Add(new ProviderModel { Id = shortName, Name = m.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? shortName : shortName, Description = desc ?? "" });
                    }
                }

                return new OkObjectResult(models.OrderBy(m => m.Name).ToList());
            }
            catch
            {
                return new OkObjectResult(GetGeminiFallbackModels());
            }
        }

        private static async Task<ActionResult> FetchGroqModels(HttpClient http, string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new BadRequestObjectResult(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
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

                return new OkObjectResult(models.OrderBy(m => m.Id).ToList());
            }
            catch
            {
                return new OkObjectResult(GetGroqFallbackModels());
            }
        }

        private static async Task<ActionResult> FetchOpenAiModels(HttpClient http, string apiKey)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new BadRequestObjectResult(await response.Content.ReadAsStringAsync());

                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
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

                return new OkObjectResult(models.Where(m => m.Id.StartsWith("gpt") || m.Id.StartsWith("o1") || m.Id.StartsWith("o3")).OrderBy(m => m.Id).ToList());
            }
            catch
            {
                return new OkObjectResult(GetOpenAiFallbackModels());
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
