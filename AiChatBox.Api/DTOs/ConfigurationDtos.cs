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
        public DateTime CreatedAt { get; set; }
        public int ApiKeyCount { get; set; }
    }

    public class ConfigurationDetailDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public string? GeminiApiKey { get; set; }
        public string? GroqApiKey { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-1.5-flash";
        public bool LiveVoiceEnabled { get; set; }
        public string? EnabledModels { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateConfigurationDto
    {
        public string Name { get; set; } = "New Configuration";
        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-1.5-flash";
    }

    public class UpdateConfigurationDto
    {
        public string? Name { get; set; }
        public string? SystemPrompt { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? GroqApiKey { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public bool? LiveVoiceEnabled { get; set; }
        public string? EnabledModels { get; set; }
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
}
