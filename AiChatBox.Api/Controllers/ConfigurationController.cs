using System.Security.Claims;
using System.Text.Json;
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
    public class ConfigurationController(ChatDbContext db, EncryptionService encryption, LlmProviderFactory providerFactory) : ControllerBase
    {
        private readonly ChatDbContext _db = db;
        private readonly EncryptionService _encryption = encryption;
        private readonly LlmProviderFactory _providerFactory = providerFactory;
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
                    HasAnthropicKey = c.AnthropicApiKey != null,
                    ConfiguredProviders = c.ProviderKeysJson,
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

            // Build a map of which OpenAI-compatible providers have keys configured
            string? configuredProviders = null;
            if (!string.IsNullOrEmpty(config.ProviderKeysJson))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ProviderKeysJson);
                    if (keys != null)
                    {
                        var providerStatus = keys.ToDictionary(k => k.Key, k => !string.IsNullOrEmpty(k.Value));
                        configuredProviders = JsonSerializer.Serialize(providerStatus);
                    }
                }
                catch { }
            }

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
                HasAnthropicKey = config.AnthropicApiKey != null,
                ConfiguredProviders = configuredProviders,
                DefaultProvider = config.DefaultProvider,
                DefaultModel = config.DefaultModel,
                LiveVoiceEnabled = config.LiveVoiceEnabled,
                RateLimitRequests = config.RateLimitRequests,
                RateLimitWindowMinutes = config.RateLimitWindowMinutes,
                MaxSpendLimit = config.MaxSpendLimit,
                CurrentSpend = config.CurrentSpend,
                SuggestionsJson = config.SuggestionsJson,
                LogRetentionDays = config.LogRetentionDays,
                MaxLogsPerSession = config.MaxLogsPerSession,
                MaxSessionsPerProject = config.MaxSessionsPerProject,
                CustomProviderName = config.CustomProviderName,
                CustomProviderBaseUrl = config.CustomProviderBaseUrl,
                HasCustomProviderKey = config.CustomProviderApiKey != null,
                PromptTemplateVariablesJson = config.PromptTemplateVariablesJson,
                HandoffEnabled = config.HandoffEnabled,
                HandoffTriggerKeywords = config.HandoffTriggerKeywords,
                HandoffEscalationCriteria = config.HandoffEscalationCriteria,
                HandoffConfidenceThreshold = config.HandoffConfidenceThreshold,
                HandoffQueueMessage = config.HandoffQueueMessage,
                ThemeSettingsJson = config.ThemeSettingsJson,
                ChannelSettingsJson = config.ChannelSettingsJson,
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

            // ── Auto-snapshot history before applying prompt/model changes ──
            bool promptChanged = model.SystemPrompt != null && model.SystemPrompt != config.SystemPrompt;
            bool modelChanged = (model.DefaultModel != null && model.DefaultModel != config.DefaultModel)
                             || (model.DefaultProvider != null && model.DefaultProvider != config.DefaultProvider);

            if (promptChanged || modelChanged)
            {
                _db.ConfigurationHistories.Add(new ConfigurationHistory
                {
                    ConfigurationId = config.Id,
                    SystemPrompt = config.SystemPrompt,
                    DefaultModel = config.DefaultModel,
                    DefaultProvider = config.DefaultProvider,
                    ChangeNote = model.ChangeNote,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (model.Name != null) config.Name = model.Name;
            if (model.SystemPrompt != null) config.SystemPrompt = model.SystemPrompt;

            // Encrypt API keys before storing
            if (model.GeminiApiKey != null) config.GeminiApiKey = model.GeminiApiKey == "" ? null : _encryption.Encrypt(model.GeminiApiKey);
            if (model.GroqApiKey != null) config.GroqApiKey = model.GroqApiKey == "" ? null : _encryption.Encrypt(model.GroqApiKey);
            if (model.OpenAiApiKey != null) config.OpenAiApiKey = model.OpenAiApiKey == "" ? null : _encryption.Encrypt(model.OpenAiApiKey);
            if (model.FirecrawlApiKey != null) config.FirecrawlApiKey = model.FirecrawlApiKey == "" ? null : _encryption.Encrypt(model.FirecrawlApiKey);
            if (model.AnthropicApiKey != null) config.AnthropicApiKey = model.AnthropicApiKey == "" ? null : _encryption.Encrypt(model.AnthropicApiKey);

            // Handle provider keys JSON (for OpenAI-compatible providers like Together, Fireworks, etc.)
            if (model.ProviderKeys != null)
            {
                try
                {
                    var incomingKeys = JsonSerializer.Deserialize<Dictionary<string, string>>(model.ProviderKeys);
                    if (incomingKeys != null)
                    {
                        // Load existing keys
                        var existingKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrEmpty(config.ProviderKeysJson))
                        {
                            try 
                            { 
                                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ProviderKeysJson);
                                if (parsed != null) existingKeys = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
                            }
                            catch { }
                        }

                        foreach (var (key, value) in incomingKeys)
                        {
                            if (string.IsNullOrEmpty(value))
                                existingKeys.Remove(key);
                            else
                                existingKeys[key] = _encryption.Encrypt(value);
                        }

                        config.ProviderKeysJson = existingKeys.Count > 0 ? JsonSerializer.Serialize(existingKeys) : null;
                    }
                }
                catch { }
            }

            // Handle custom provider
            if (model.CustomProviderName != null) config.CustomProviderName = string.IsNullOrEmpty(model.CustomProviderName) ? null : model.CustomProviderName;
            if (model.CustomProviderBaseUrl != null) config.CustomProviderBaseUrl = string.IsNullOrEmpty(model.CustomProviderBaseUrl) ? null : model.CustomProviderBaseUrl;
            if (model.CustomProviderApiKey != null) config.CustomProviderApiKey = model.CustomProviderApiKey == "" ? null : _encryption.Encrypt(model.CustomProviderApiKey);

            if (model.DefaultProvider != null) config.DefaultProvider = model.DefaultProvider;
            if (model.DefaultModel != null) config.DefaultModel = model.DefaultModel;
            if (model.LiveVoiceEnabled.HasValue) config.LiveVoiceEnabled = model.LiveVoiceEnabled.Value;
            if (model.EnabledModels != null) config.EnabledModels = model.EnabledModels;

            if (model.RateLimitRequests.HasValue) config.RateLimitRequests = model.RateLimitRequests.Value;
            if (model.RateLimitWindowMinutes.HasValue) config.RateLimitWindowMinutes = model.RateLimitWindowMinutes.Value;
            if (model.MaxSpendLimit.HasValue) config.MaxSpendLimit = model.MaxSpendLimit.Value;
            if (model.SuggestionsJson != null) config.SuggestionsJson = model.SuggestionsJson;
            if (model.LogRetentionDays.HasValue) config.LogRetentionDays = model.LogRetentionDays.Value;
            if (model.MaxLogsPerSession.HasValue) config.MaxLogsPerSession = model.MaxLogsPerSession.Value;
            if (model.MaxSessionsPerProject.HasValue) config.MaxSessionsPerProject = model.MaxSessionsPerProject.Value;
            if (model.PromptTemplateVariablesJson != null) config.PromptTemplateVariablesJson = string.IsNullOrEmpty(model.PromptTemplateVariablesJson) ? null : model.PromptTemplateVariablesJson;
            if (model.HandoffEnabled.HasValue) config.HandoffEnabled = model.HandoffEnabled.Value;
            if (model.HandoffTriggerKeywords != null) config.HandoffTriggerKeywords = string.IsNullOrEmpty(model.HandoffTriggerKeywords) ? null : model.HandoffTriggerKeywords;
            if (model.HandoffEscalationCriteria != null) config.HandoffEscalationCriteria = string.IsNullOrEmpty(model.HandoffEscalationCriteria) ? null : model.HandoffEscalationCriteria;
            if (model.HandoffConfidenceThreshold.HasValue) config.HandoffConfidenceThreshold = model.HandoffConfidenceThreshold.Value;
            if (model.HandoffQueueMessage != null) config.HandoffQueueMessage = string.IsNullOrEmpty(model.HandoffQueueMessage) ? null : model.HandoffQueueMessage;
            if (model.ThemeSettingsJson != null) config.ThemeSettingsJson = string.IsNullOrEmpty(model.ThemeSettingsJson) ? null : model.ThemeSettingsJson;
            if (model.ChannelSettingsJson != null) config.ChannelSettingsJson = string.IsNullOrEmpty(model.ChannelSettingsJson) ? null : model.ChannelSettingsJson;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("configuration/{id}/history")]
        public async Task<ActionResult<IEnumerable<ConfigurationHistoryDto>>> GetConfigurationHistory(Guid id)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            var history = await _db.ConfigurationHistories
                .Where(h => h.ConfigurationId == id)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new ConfigurationHistoryDto
                {
                    Id = h.Id,
                    SystemPrompt = h.SystemPrompt,
                    DefaultProvider = h.DefaultProvider,
                    DefaultModel = h.DefaultModel,
                    ChangeNote = h.ChangeNote,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return Ok(history);
        }

        [HttpPost("configuration/{id}/history/{historyId}/restore")]
        public async Task<IActionResult> RestoreConfiguration(Guid id, Guid historyId)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);

            if (config == null) return NotFound();

            var history = await _db.ConfigurationHistories
                .FirstOrDefaultAsync(h => h.Id == historyId && h.ConfigurationId == id);

            if (history == null) return NotFound();

            // Create a snapshot of current state before restoring
            _db.ConfigurationHistories.Add(new ConfigurationHistory
            {
                ConfigurationId = config.Id,
                SystemPrompt = config.SystemPrompt,
                DefaultModel = config.DefaultModel,
                DefaultProvider = config.DefaultProvider,
                ChangeNote = $"Auto-save before restoring to {history.CreatedAt:g}",
                CreatedAt = DateTime.UtcNow
            });

            // Restore values
            config.SystemPrompt = history.SystemPrompt;
            config.DefaultProvider = history.DefaultProvider;
            config.DefaultModel = history.DefaultModel;

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
                "anthropic" => config.AnthropicApiKey,
                _ => null
            };

            // If not a named key, check ProviderKeysJson
            if (string.IsNullOrEmpty(encryptedKey) && !string.IsNullOrEmpty(config.ProviderKeysJson))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ProviderKeysJson);
                    if (keys != null && keys.TryGetValue(provider.ToLowerInvariant(), out var pk))
                        encryptedKey = pk;
                }
                catch { }
            }

            if (string.IsNullOrEmpty(encryptedKey))
                return BadRequest($"No API key configured for provider '{provider}'.");

            var apiKey = _encryption.Decrypt(encryptedKey);

            var httpClient = HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();

            // Check if it's an OpenAI-compatible provider — they all share the same model list format
            var providerInfo = ProviderRegistry.GetProvider(provider);
            if (providerInfo != null && providerInfo.IsOpenAiCompatible)
            {
                return await FetchOpenAiCompatibleModels(httpClient, apiKey!, providerInfo.BaseUrl, providerInfo.Name);
            }

            return provider.ToLowerInvariant() switch
            {
                "gemini" => await FetchGeminiModels(httpClient, apiKey!),
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

        /// <summary>
        /// Returns the list of all known AI providers from the registry.
        /// </summary>
        [HttpGet("providers")]
        public ActionResult GetProviders()
        {
            var providers = ProviderRegistry.GetAllProviders().Select(p => new
            {
                p.Id,
                p.Name,
                p.DefaultModel,
                p.IsOpenAiCompatible
            });
            return Ok(providers);
        }

        /// <summary>
        /// Returns prompt version history for a configuration (newest first).
        /// </summary>
        [HttpGet("configuration/{id}/history")]
        public async Task<ActionResult<IEnumerable<ConfigurationHistoryDto>>> GetHistory(Guid id, [FromQuery] int limit = 50)
        {
            var config = await _db.Configurations
                .AnyAsync(c => c.Id == id && c.Project!.UserId == UserId);
            if (!config) return NotFound();

            var history = await _db.ConfigurationHistories
                .Where(h => h.ConfigurationId == id)
                .OrderByDescending(h => h.CreatedAt)
                .Take(limit)
                .Select(h => new ConfigurationHistoryDto
                {
                    Id = h.Id,
                    SystemPrompt = h.SystemPrompt,
                    DefaultModel = h.DefaultModel,
                    DefaultProvider = h.DefaultProvider,
                    ChangeNote = h.ChangeNote,
                    CreatedAt = h.CreatedAt
                })
                .ToListAsync();

            return Ok(history);
        }

        /// <summary>
        /// Restores a previous prompt version. The current state is snapshot'd before restoring.
        /// </summary>
        [HttpPost("configuration/{id}/history/{historyId}/restore")]
        public async Task<IActionResult> RestoreHistory(Guid id, Guid historyId)
        {
            var config = await _db.Configurations
                .FirstOrDefaultAsync(c => c.Id == id && c.Project!.UserId == UserId);
            if (config == null) return NotFound();

            var historyEntry = await _db.ConfigurationHistories
                .FirstOrDefaultAsync(h => h.Id == historyId && h.ConfigurationId == id);
            if (historyEntry == null) return NotFound("History entry not found.");

            // Snapshot current state before restoring
            _db.ConfigurationHistories.Add(new ConfigurationHistory
            {
                ConfigurationId = config.Id,
                SystemPrompt = config.SystemPrompt,
                DefaultModel = config.DefaultModel,
                DefaultProvider = config.DefaultProvider,
                ChangeNote = "Auto-saved before restore",
                CreatedAt = DateTime.UtcNow
            });

            // Restore
            config.SystemPrompt = historyEntry.SystemPrompt;
            config.DefaultModel = historyEntry.DefaultModel;
            config.DefaultProvider = historyEntry.DefaultProvider;

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

        /// <summary>
        /// Fetches models from any OpenAI-compatible API (Groq, Together, Fireworks, Mistral, etc.)
        /// </summary>
        private static async Task<ActionResult> FetchOpenAiCompatibleModels(HttpClient http, string apiKey, string baseUrl, string providerName)
        {
            try
            {
                var url = $"{baseUrl.TrimEnd('/')}/models";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
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
                        var id = m.GetProperty("id").GetString() ?? "";
                        models.Add(new ProviderModel
                        {
                            Id = id,
                            Name = id,
                            Description = m.TryGetProperty("owned_by", out var owned) ? owned.GetString() ?? "" : ""
                        });
                    }
                }

                return new OkObjectResult(models.OrderBy(m => m.Id).ToList());
            }
            catch
            {
                return new OkObjectResult(new List<ProviderModel>());
            }
        }

        private static List<ProviderModel> GetGeminiFallbackModels() =>
        [
            new() { Id = "gemini-1.5-flash", Name = "Gemini 1.5 Flash", Description = "Fast and efficient for most tasks" },
            new() { Id = "gemini-1.5-pro", Name = "Gemini 1.5 Pro", Description = "Advanced reasoning capabilities" },
            new() { Id = "gemini-2.0-flash", Name = "Gemini 2.0 Flash", Description = "Next-gen speed and quality" },
            new() { Id = "gemini-2.5-flash-preview-06-17", Name = "Gemini 2.5 Flash", Description = "Latest preview with enhanced reasoning" }
        ];
    }
}
