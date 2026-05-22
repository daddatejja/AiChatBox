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


    public enum UserRole
    {
        StandardUser,       // Direct customer — manages their own projects
        PartnerDeveloper,   // B2B integrator — programmatically provisions tenants
        SystemAdmin         // Platform operator — sees everything
    }

    public class PartnerAccount
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(200)]
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        public string OwnerId { get; set; } = string.Empty;       // FK → ApplicationUser.Id
        public ApplicationUser? Owner { get; set; }

        [MaxLength(500)]
        public string? AllowedDomainPattern { get; set; }          // e.g., "*.partnerapp.com"

        // Limits
        public int MaxTenants { get; set; } = 100;
        public decimal CreditLimit { get; set; } = 0;              // 0 = unlimited
        public decimal CurrentSpend { get; set; } = 0;

        // Default template for new tenant projects
        public string? DefaultSystemPrompt { get; set; }
        public string? DefaultProvider { get; set; }
        public string? DefaultModel { get; set; }
        public string? DefaultThemeSettingsJson { get; set; }

        // Master API Key (hashed, like ApiKey model)
        [Required]
        public string MasterKeyHash { get; set; } = string.Empty;
        public bool MasterKeyActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Project> TenantProjects { get; set; } = [];
    }

    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Project> Projects { get; set; } = [];
        public UserRole AccountType { get; set; } = UserRole.StandardUser;
        public Guid? PartnerAccountId { get; set; }  // FK to PartnerAccount (if PartnerDeveloper)
        public PartnerAccount? PartnerAccount { get; set; }
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

        // B2B Multi-tenancy support
        public Guid? PartnerAccountId { get; set; }  // null for direct customers
        public PartnerAccount? PartnerAccount { get; set; }

        [MaxLength(200)]
        public string? TenantIdentifier { get; set; }  // e.g., subdomain or external tenant ID

        [MaxLength(2000)]
        public string EmbedSettingsJson { get; set; } = "{\"showPrompt\":true,\"showKnowledgeBase\":true,\"showRules\":true,\"showWidgetCustomization\":true}";

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

        /// <summary>keyword, regex, exact, intent, or command</summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "keyword";

        /// <summary>
        /// For keyword/regex/exact: the trigger pattern.
        /// For intent: a plain-English description of the user's intent.
        /// Example: "User is asking about pricing or subscription plans"
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Trigger { get; set; } = string.Empty;

        /// <summary>
        /// A short label for this intent (used as the intent ID in LLM classification).
        /// Example: "pricing", "refund_request", "business_hours"
        /// </summary>
        [MaxLength(100)]
        public string? IntentLabel { get; set; }

        // ── Command-type fields ────────────────────────────────────────────────

        /// <summary>
        /// For command-type rules: the command name users type after the trigger char.
        /// Example: "pricing" (user types "/pricing").
        /// </summary>
        [MaxLength(100)]
        public string? CommandName { get; set; }

        /// <summary>
        /// The special character that prefixes the command. Defaults to "/".
        /// Allowed: "/", "#", "@".
        /// </summary>
        [MaxLength(1)]
        public string CommandTriggerChar { get; set; } = "/";

        /// <summary>
        /// Short description shown in the widget autocomplete popup.
        /// Example: "Get pricing and subscription info"
        /// </summary>
        [MaxLength(200)]
        public string? CommandDescription { get; set; }

        // ── Rich response fields ───────────────────────────────────────────────

        /// <summary>
        /// Determines how the response is delivered.
        /// Values: text | redirect | card | ai | file | form | tool_call
        /// Defaults to "text" for backward compatibility.
        /// </summary>
        [MaxLength(20)]
        public string ResponseType { get; set; } = "text";

        /// <summary>
        /// Structured JSON payload for non-text response types.
        /// - redirect:  { "url": "https://..." }
        /// - card:      { "title": "...", "body": "...", "buttonLabel": "...", "buttonUrl": "..." }
        /// - ai:        Additional system prompt text (plain string, not JSON)
        /// - file:      { "fileUrl": "...", "fileName": "...", "mimeType": "..." }
        /// - form:      { "fields": [{"name","label","type","required"}], "webhookUrl": "...", "submitLabel": "..." }
        /// - tool_call: { "toolName": "...", "parameters": { ... } }
        /// </summary>
        public string? ResponsePayload { get; set; }

        /// <summary>The static text response (used when ResponseType = "text").</summary>
        [Required]
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// Minimum confidence score (0.0 to 1.0) required to trigger this rule.
        /// Only used for intent-type rules. Lower = more sensitive, higher = more precise.
        /// </summary>
        public double ConfidenceThreshold { get; set; } = 0.75;

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

        public string? AllowedTables { get; set; }

        /// <summary>
        /// JSON map of table → allowed column list (null = all columns allowed).
        /// Shape: { "orders": ["id", "total"], "users": null }
        /// Enforced at query time in UserSqlTool (Strategy A: rejects SELECT * for restricted tables).
        /// </summary>
        public string? AllowedColumnsJson { get; set; }

        public int MaxQueryTimeoutSeconds { get; set; } = 5;
        public int MaxRecordsPerQuery { get; set; } = 100;
        public string? SessionContextFilterJson { get; set; }

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
        /// Kept for backward compatibility and as a fast-path check.
        /// </summary>
        [MaxLength(1000)]
        public string? HandoffTriggerKeywords { get; set; }

        /// <summary>
        /// Plain-English description of when to escalate to a human agent.
        /// Used for LLM-powered intent classification when keywords don't match.
        /// Example: "User is frustrated, requests human help, or has a billing dispute"
        /// </summary>
        [MaxLength(2000)]
        public string? HandoffEscalationCriteria { get; set; }

        /// <summary>
        /// Confidence threshold for LLM-based escalation detection (0.0-1.0).
        /// Default 0.7 for slightly more sensitive triggering.
        /// </summary>
        public double HandoffConfidenceThreshold { get; set; } = 0.7;

        /// <summary>Message shown to the user when placed in queue.</summary>
        [MaxLength(500)]
        public string? HandoffQueueMessage { get; set; }

        /// <summary>
        /// JSON object containing widget theme configuration (colors, fonts, position).
        /// </summary>
        [MaxLength(2000)]
        public string? ThemeSettingsJson { get; set; }

        /// <summary>
        /// Encrypted JSON holding WhatsApp, Slack, Telegram credentials
        /// </summary>
        [MaxLength(4000)]
        public string? ChannelSettingsJson { get; set; }
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

        public int ChunkSize { get; set; } = 1000;
        public int ChunkOverlap { get; set; } = 200;

        [MaxLength(50)]
        public string ChunkingStrategy { get; set; } = "character";

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
