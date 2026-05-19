using System.Text.Json;
using AiChatBox.Api.Interfaces;
using AiChatBox.Api.Models;

namespace AiChatBox.Api.Services
{
    /// <summary>
    /// Dynamically resolves an ILlmProviderService based on provider name.
    /// Supports Gemini (native API), Anthropic (Claude API), and any OpenAI-compatible provider
    /// using the generic OpenAiCompatibleService with configurable base URLs.
    /// </summary>
    public class LlmProviderFactory
    {
        private readonly GeminiServerService _geminiService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EncryptionService _encryption;
        private readonly ILogger<OpenAiCompatibleService> _openAiLogger;

        public LlmProviderFactory(
            GeminiServerService geminiService,
            IHttpClientFactory httpClientFactory,
            EncryptionService encryption,
            ILogger<OpenAiCompatibleService> openAiLogger)
        {
            _geminiService = geminiService;
            _httpClientFactory = httpClientFactory;
            _encryption = encryption;
            _openAiLogger = openAiLogger;
        }

        /// <summary>
        /// Gets a provider service using global defaults. Use this for simple cases
        /// where no per-configuration API key override is needed.
        /// </summary>
        public ILlmProviderService GetProvider(string providerName)
        {
            if (string.IsNullOrEmpty(providerName) || providerName.Equals("gemini", StringComparison.OrdinalIgnoreCase))
                return _geminiService;

            var providerInfo = ProviderRegistry.GetProvider(providerName);
            if (providerInfo != null && providerInfo.IsOpenAiCompatible)
            {
                return CreateOpenAiCompatibleService(providerInfo.BaseUrl, "", providerInfo.DefaultModel, providerInfo.Name);
            }

            // Fallback to Gemini for unknown providers
            return _geminiService;
        }

        /// <summary>
        /// Gets a provider service with a specific API key and optional configuration context.
        /// This is the primary method used during chat — it resolves the correct provider
        /// and injects the right API key from the project configuration.
        /// </summary>
        public ILlmProviderService GetProvider(string providerName, string? apiKey, ProjectConfiguration? config = null)
        {
            if (string.IsNullOrEmpty(providerName) || providerName.Equals("gemini", StringComparison.OrdinalIgnoreCase))
                return _geminiService;

            // Check known providers first
            var providerInfo = ProviderRegistry.GetProvider(providerName);
            if (providerInfo != null && providerInfo.IsOpenAiCompatible)
            {
                return CreateOpenAiCompatibleService(providerInfo.BaseUrl, apiKey ?? "", providerInfo.DefaultModel, providerInfo.Name);
            }

            // Check if it's a custom provider from configuration
            if (config != null && !string.IsNullOrEmpty(config.CustomProviderName) 
                && providerName.Equals(config.CustomProviderName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(config.CustomProviderBaseUrl))
            {
                var customApiKey = apiKey ?? _encryption.Decrypt(config.CustomProviderApiKey) ?? "";
                return CreateOpenAiCompatibleService(config.CustomProviderBaseUrl, customApiKey, "default", config.CustomProviderName);
            }

            // Fallback to Gemini
            return _geminiService;
        }

        /// <summary>
        /// Resolves the encrypted API key for a given provider from a ProjectConfiguration.
        /// Returns the decrypted key, or null if no key is configured.
        /// </summary>
        public string? ResolveApiKey(string providerName, ProjectConfiguration? config)
        {
            if (config == null) return null;

            // Check named key fields first
            var encryptedKey = providerName.ToLowerInvariant() switch
            {
                "gemini" => config.GeminiApiKey,
                "groq" => config.GroqApiKey,
                "openai" => config.OpenAiApiKey,
                "anthropic" => config.AnthropicApiKey,
                _ => null
            };

            if (!string.IsNullOrEmpty(encryptedKey))
                return _encryption.Decrypt(encryptedKey);

            // Check ProviderKeysJson for OpenAI-compatible providers
            if (!string.IsNullOrEmpty(config.ProviderKeysJson))
            {
                try
                {
                    var keys = JsonSerializer.Deserialize<Dictionary<string, string>>(config.ProviderKeysJson);
                    if (keys != null)
                    {
                        var caseInsensitiveKeys = new Dictionary<string, string>(keys, StringComparer.OrdinalIgnoreCase);
                        if (caseInsensitiveKeys.TryGetValue(providerName, out var encrypted))
                        {
                            return _encryption.Decrypt(encrypted);
                        }
                    }
                }
                catch { }
            }

            // Check custom provider
            if (!string.IsNullOrEmpty(config.CustomProviderName) 
                && providerName.Equals(config.CustomProviderName, StringComparison.OrdinalIgnoreCase))
            {
                return _encryption.Decrypt(config.CustomProviderApiKey);
            }

            return null;
        }

        /// <summary>
        /// Checks whether a given provider has an API key configured.
        /// </summary>
        public bool HasApiKey(string providerName, ProjectConfiguration? config)
        {
            return !string.IsNullOrEmpty(ResolveApiKey(providerName, config));
        }

        private OpenAiCompatibleService CreateOpenAiCompatibleService(string baseUrl, string apiKey, string defaultModel, string providerName)
        {
            var client = _httpClientFactory.CreateClient();
            return new OpenAiCompatibleService(client, baseUrl, apiKey, defaultModel, providerName, _openAiLogger);
        }
    }
}
