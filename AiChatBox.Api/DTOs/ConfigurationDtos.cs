namespace AiChatBox.Api.DTOs
{
    public class ConfigurationDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-1.5-flash";
        public bool LiveVoiceEnabled { get; set; }
        public bool HasGeminiKey { get; set; }
        public bool HasGroqKey { get; set; }
        public bool HasOpenAiKey { get; set; }
        public bool HasFirecrawlKey { get; set; }
        public bool HasAnthropicKey { get; set; }
        /// <summary>JSON object with provider IDs as keys and true/false for whether a key is set.</summary>
        public string? ConfiguredProviders { get; set; }
        public int RateLimitRequests { get; set; }
        public decimal MaxSpendLimit { get; set; }
        public decimal CurrentSpend { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ApiKeyCount { get; set; }
    }

    public class ConfigurationDetailDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public bool HasGeminiKey { get; set; }
        public bool HasGroqKey { get; set; }
        public bool HasOpenAiKey { get; set; }
        public bool HasFirecrawlKey { get; set; }
        public bool HasAnthropicKey { get; set; }
        /// <summary>JSON object mapping provider IDs to whether a key is configured (e.g. {"together": true, "fireworks": false})</summary>
        public string? ConfiguredProviders { get; set; }
        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-1.5-flash";
        public bool LiveVoiceEnabled { get; set; }
        public string? EnabledModels { get; set; }
        public int RateLimitRequests { get; set; }
        public int RateLimitWindowMinutes { get; set; }
        public decimal MaxSpendLimit { get; set; }
        public decimal CurrentSpend { get; set; }
        public string? SuggestionsJson { get; set; }
        public int LogRetentionDays { get; set; }
        public int MaxLogsPerSession { get; set; }
        public int MaxSessionsPerProject { get; set; }
        public string? CustomProviderName { get; set; }
        public string? CustomProviderBaseUrl { get; set; }
        public bool HasCustomProviderKey { get; set; }
        public string? PromptTemplateVariablesJson { get; set; }
        public bool HandoffEnabled { get; set; }
        public string? HandoffTriggerKeywords { get; set; }
        public string? HandoffQueueMessage { get; set; }
        public string? ThemeSettingsJson { get; set; }
        public string? ChannelSettingsJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateConfigurationDto
    {
        public string Name { get; set; } = "New Configuration";
        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-1.5-flash";
        public bool HandoffEnabled { get; set; }
    }

    public class UpdateConfigurationDto
    {
        public string? Name { get; set; }
        public string? SystemPrompt { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? GroqApiKey { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string? FirecrawlApiKey { get; set; }
        public string? AnthropicApiKey { get; set; }
        /// <summary>JSON object with provider IDs as keys and API key values (e.g. {"together": "key123"}). Empty string values remove the key.</summary>
        public string? ProviderKeys { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public bool? LiveVoiceEnabled { get; set; }
        public string? EnabledModels { get; set; }
        public int? RateLimitRequests { get; set; }
        public int? RateLimitWindowMinutes { get; set; }
        public decimal? MaxSpendLimit { get; set; }
        public string? SuggestionsJson { get; set; }
        public int? LogRetentionDays { get; set; }
        public int? MaxLogsPerSession { get; set; }
        public int? MaxSessionsPerProject { get; set; }
        public string? CustomProviderName { get; set; }
        public string? CustomProviderBaseUrl { get; set; }
        public string? CustomProviderApiKey { get; set; }
        public string? PromptTemplateVariablesJson { get; set; }
        public string? ChangeNote { get; set; }
        public bool? HandoffEnabled { get; set; }
        public string? HandoffTriggerKeywords { get; set; }
        public string? HandoffQueueMessage { get; set; }
        public string? ThemeSettingsJson { get; set; }
        public string? ChannelSettingsJson { get; set; }
    }

    public class ProviderModelsRequest
    {
        public string Provider { get; set; } = "gemini";
        public string ApiKey { get; set; } = string.Empty;
    }

    public class ProviderModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ConfigurationHistoryDto
    {
        public Guid Id { get; set; }
        public string SystemPrompt { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = string.Empty;
        public string DefaultProvider { get; set; } = string.Empty;
        public string? ChangeNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
