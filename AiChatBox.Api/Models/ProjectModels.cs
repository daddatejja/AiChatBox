using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Pgvector;

namespace AiChatBox.Api.Models
{
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

        public ICollection<ApiKey> ApiKeys { get; set; } = [];
        public ICollection<CustomTool> CustomTools { get; set; } = [];
        public ICollection<ChatSession> Sessions { get; set; } = [];
        public ICollection<ProjectConfiguration> Configurations { get; set; } = [];
        public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = [];
        public ICollection<WebsiteCrawlJob> WebsiteCrawlJobs { get; set; } = [];
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
