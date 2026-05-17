using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Pgvector;

namespace AiChatBox.Api.Models
{
    /// <summary>
    /// Registry of known AI providers with their base URLs and default models.
    /// Providers using the OpenAI-compatible chat completions API can be targeted
    /// with a single generic service implementation.
    /// </summary>
    public static class ProviderRegistry
    {
        public record ProviderInfo(string Id, string Name, string BaseUrl, string DefaultModel, bool IsOpenAiCompatible);

        public static readonly Dictionary<string, ProviderInfo> KnownProviders = new(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini"] = new("gemini", "Google Gemini", "https://generativelanguage.googleapis.com", "gemini-3.1-flash-lite-preview", false),
            ["openai"] = new("openai", "OpenAI", "https://api.openai.com/v1", "gpt-4o-mini", true),
            ["groq"] = new("groq", "Groq", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", true),
            ["anthropic"] = new("anthropic", "Anthropic Claude", "https://api.anthropic.com", "claude-sonnet-4-20250514", false),
            ["together"] = new("together", "Together AI", "https://api.together.xyz/v1", "meta-llama/Llama-3.3-70B-Instruct-Turbo", true),
            ["fireworks"] = new("fireworks", "Fireworks AI", "https://api.fireworks.ai/inference/v1", "accounts/fireworks/models/llama-v3p3-70b-instruct", true),
            ["mistral"] = new("mistral", "Mistral AI", "https://api.mistral.ai/v1", "mistral-small-latest", true),
            ["openrouter"] = new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "meta-llama/llama-3.3-70b-instruct", true),
            ["deepinfra"] = new("deepinfra", "DeepInfra", "https://api.deepinfra.com/v1/openai", "meta-llama/Llama-3.3-70B-Instruct", true),
            ["cerebras"] = new("cerebras", "Cerebras", "https://api.cerebras.ai/v1", "llama-3.3-70b", true),
            ["sambanova"] = new("sambanova", "SambaNova", "https://api.sambanova.ai/v1", "Meta-Llama-3.3-70B-Instruct", true),
        };

        /// <summary>
        /// Returns all providers suitable for display in UI dropdowns.
        /// Includes known providers plus any custom provider the user has configured.
        /// </summary>
        public static IEnumerable<ProviderInfo> GetAllProviders()
        {
            return KnownProviders.Values;
        }

        /// <summary>
        /// Resolves a provider by ID. Returns null if not found.
        /// </summary>
        public static ProviderInfo? GetProvider(string providerId)
        {
            return KnownProviders.TryGetValue(providerId, out var info) ? info : null;
        }
    }


    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Project> Projects { get; set; } = [];
    }

    public enum KnowledgeDocumentStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public enum DatabaseType
    {
        PostgreSQL,
        MySQL,
        SQLite,
        SQLServer
    }

    public class Project
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";
        
        public string Provider { get; set; } = "gemini";
        public string ModelName { get; set; } = "gemini-3.1-flash-lite-preview";

        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }

        public string? AllowedDomains { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ProjectDatabase? Database { get; set; }

        public ICollection<ApiKey> ApiKeys { get; set; } = [];
        public ICollection<CustomTool> CustomTools { get; set; } = [];
        public ICollection<ChatSession> Sessions { get; set; } = [];
        public ICollection<ProjectConfiguration> Configurations { get; set; } = [];
        public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = [];
        public ICollection<WebsiteCrawlJob> WebsiteCrawlJobs { get; set; } = [];
        public ICollection<ConversationRule> ConversationRules { get; set; } = [];
    }

    /// <summary>
    /// A rule that intercepts user messages before they reach an LLM provider.
    /// Enables zero-cost, instant responses for common queries.
    /// </summary>
    public class ConversationRule
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>keyword, regex, or exact</summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "keyword";

        /// <summary>The trigger pattern: keyword(s), regex pattern, or exact question text.</summary>
        [Required]
        [MaxLength(1000)]
        public string Trigger { get; set; } = string.Empty;

        /// <summary>The static response to send when the rule matches.</summary>
        [Required]
        public string Response { get; set; } = string.Empty;

        /// <summary>Higher priority rules are checked first.</summary>
        public int Priority { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProjectDatabase
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public DatabaseType Type { get; set; } = DatabaseType.PostgreSQL;

        public string? ConnectionString { get; set; } // Encrypted
        
        public string? SchemaDefinition { get; set; } // DDL text

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class WebsiteCrawlJob
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        public string BaseUrl { get; set; } = string.Empty;

        public int MaxPages { get; set; } = 10; // Default limit for safety
        public int PagesCrawled { get; set; } = 0;
        public KnowledgeDocumentStatus Status { get; set; } = KnowledgeDocumentStatus.Pending;
        public string? FirecrawlJobId { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProjectConfiguration
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Default";

        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";

        public string? GeminiApiKey { get; set; }
        public string? GroqApiKey { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string? FirecrawlApiKey { get; set; }
        public string? AnthropicApiKey { get; set; }

        /// <summary>
        /// Stores API keys for OpenAI-compatible providers as a JSON object:
        /// { "together": "encrypted-key", "fireworks": "encrypted-key", ... }
        /// </summary>
        [MaxLength(4000)]
        public string? ProviderKeysJson { get; set; }

        /// <summary>
        /// Custom OpenAI-compatible provider name (e.g. "my-provider")
        /// </summary>
        [MaxLength(100)]
        public string? CustomProviderName { get; set; }

        /// <summary>
        /// Custom OpenAI-compatible provider base URL (e.g. "https://my-api.com/v1")
        /// </summary>
        [MaxLength(500)]
        public string? CustomProviderBaseUrl { get; set; }

        /// <summary>
        /// Encrypted API key for the custom provider.
        /// </summary>
        public string? CustomProviderApiKey { get; set; }

        public string DefaultProvider { get; set; } = "gemini";
        public string DefaultModel { get; set; } = "gemini-3.1-flash-lite-preview";

        public bool LiveVoiceEnabled { get; set; }

        [MaxLength(4000)]
        public string? EnabledModels { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ApiKey> ApiKeys { get; set; } = [];
        public ICollection<ChatSession> Sessions { get; set; } = [];

        // Admin Controls & Limits
        public int RateLimitRequests { get; set; } = 0; // 0 = disabled
        public int RateLimitWindowMinutes { get; set; } = 1;
        public decimal MaxSpendLimit { get; set; } = 0; // 0 = disabled
        public decimal CurrentSpend { get; set; } = 0;
        public string? SuggestionsJson { get; set; } // JSON array of strings
        public int LogRetentionDays { get; set; } = 30; // 0 = disabled/keep forever
        public int MaxLogsPerSession { get; set; } = 500;
        public int MaxSessionsPerProject { get; set; } = 50;

        /// <summary>
        /// JSON object of template variable values, e.g. {"company": "Acme Inc", "product": "WidgetPro"}
        /// Substituted into the system prompt at runtime.
        /// </summary>
        [MaxLength(4000)]
        public string? PromptTemplateVariablesJson { get; set; }

        /// <summary>Enable the human handoff feature for this configuration.</summary>
        public bool HandoffEnabled { get; set; } = false;

        /// <summary>
        /// Comma-separated keywords that trigger escalation (e.g. "human,agent,help,escalate").
        /// </summary>
        [MaxLength(1000)]
        public string? HandoffTriggerKeywords { get; set; }

        /// <summary>Message shown to the user when placed in queue.</summary>
        [MaxLength(500)]
        public string? HandoffQueueMessage { get; set; }

        /// <summary>
        /// JSON object containing widget theme configuration (colors, fonts, position).
        /// </summary>
        [MaxLength(2000)]
        public string? ThemeSettingsJson { get; set; }
    }

    public class ApiKey
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        public Guid? ConfigurationId { get; set; }
        public ProjectConfiguration? Configuration { get; set; }

        public Guid? ExperimentConfigurationId { get; set; }
        public int ExperimentWeight { get; set; } = 0; // 0-100 percentage

        [Required]
        public string KeyHash { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Label { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }

    public class ConfigurationHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ConfigurationId { get; set; }
        public ProjectConfiguration? Configuration { get; set; }

        public string SystemPrompt { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = string.Empty;
        public string DefaultProvider { get; set; } = string.Empty;

        /// <summary>Optional label describing the change, e.g. "Updated pricing instructions"</summary>
        [MaxLength(200)]
        public string? ChangeNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CustomTool
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public string ParametersJsonSchema { get; set; } = "{}";

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class KnowledgeDocument
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ProjectId { get; set; }
        public Project? Project { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public string? ContentType { get; set; }
        public long FileSize { get; set; }

        public bool IsProcessed { get; set; }
        public KnowledgeDocumentStatus Status { get; set; } = KnowledgeDocumentStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? StoredFileName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<DocumentChunk> Chunks { get; set; } = [];
    }

    public class DocumentChunk
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid DocumentId { get; set; }
        public KnowledgeDocument? Document { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [Column(TypeName = "vector(3072)")]
        public Vector? Embedding { get; set; }

        public int ChunkIndex { get; set; }
    }
}
